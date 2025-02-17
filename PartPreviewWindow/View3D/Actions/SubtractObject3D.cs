﻿/*
Copyright (c) 2017, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	[ShowUpdateButton]
	public class SubtractObject3D : MeshWrapperObject3D
	{
		public SubtractObject3D()
		{
			Name = "Subtract";
		}

		public ChildrenSelector ItemsToSubtract { get; set; } = new ChildrenSelector();

		public static void Subtract(List<IObject3D> keepObjects, List<IObject3D> removeObjects)
		{
			Subtract(keepObjects, removeObjects, CancellationToken.None, null);
		}

		public static void Subtract(List<IObject3D> keepObjects, List<IObject3D> removeObjects, CancellationToken cancellationToken, IProgress<ProgressStatus> reporter)
		{
			if (removeObjects.Any()
				&& keepObjects.Any())
			{
				var totalOperations = removeObjects.Count * keepObjects.Count;
				double amountPerOperation = 1.0 / totalOperations;
				double percentCompleted = 0;

				ProgressStatus progressStatus = new ProgressStatus();
				foreach (var remove in removeObjects.Select((r) => (obj3D: r, matrix: r.WorldMatrix())).ToList())
				{
					foreach (var keep in keepObjects.Select((r) => (obj3D: r, matrix: r.WorldMatrix())).ToList())
					{
						progressStatus.Status = "Copy Remove";
						reporter?.Report(progressStatus);
						var transformedRemove = remove.obj3D.Mesh.Copy(cancellationToken);
						transformedRemove.Transform(remove.matrix);

						progressStatus.Status = "Copy Keep";
						reporter?.Report(progressStatus);
						var transformedKeep = keep.obj3D.Mesh.Copy(cancellationToken);
						transformedKeep.Transform(keep.matrix);

						progressStatus.Status = "Do CSG";
						reporter?.Report(progressStatus);
						transformedKeep = PolygonMesh.Csg.CsgOperations.Subtract(transformedKeep, transformedRemove, (status, progress0To1) =>
						{
							// Abort if flagged
							cancellationToken.ThrowIfCancellationRequested();

							progressStatus.Status = status;
							progressStatus.Progress0To1 = percentCompleted + amountPerOperation * progress0To1;
							reporter?.Report(progressStatus);
						}, cancellationToken);
						var inverse = keep.matrix.Inverted;
						transformedKeep.Transform(inverse);

						using (keep.obj3D.RebuildLock())
						{
							keep.obj3D.Mesh = transformedKeep;
						}

						percentCompleted += amountPerOperation;
						progressStatus.Progress0To1 = percentCompleted;
						reporter?.Report(progressStatus);
					}

					remove.obj3D.Visible = false;
				}
			}
		}

		public override void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType == InvalidateType.Content
				|| invalidateType.InvalidateType == InvalidateType.Matrix
				|| invalidateType.InvalidateType == InvalidateType.Mesh)
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				Rebuild(null);
			}
			else if (invalidateType.InvalidateType == InvalidateType.Properties
				&& invalidateType.Source == this)
			{
				Rebuild(null);
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		private void Rebuild(UndoBuffer undoBuffer)
		{
			this.DebugDepth("Rebuild");
			var rebuildLock = RebuildLock();
			ResetMeshWrapperMeshes(Object3DPropertyFlags.All, CancellationToken.None);

			// spin up a task to remove holes from the objects in the group
			ApplicationController.Instance.Tasks.Execute(
				"Subtract".Localize(),
				(reporter, cancellationToken) =>
				{
					var progressStatus = new ProgressStatus();
					reporter.Report(progressStatus);

					var removeObjects = this.Children
						.Where((i) => ItemsToSubtract.Contains(i.ID))
						.SelectMany((h) => h.DescendantsAndSelf())
						.Where((c) => c.OwnerID == this.ID).ToList();
					var keepObjects = this.Children
						.Where((i) => !ItemsToSubtract.Contains(i.ID))
						.SelectMany((h) => h.DescendantsAndSelf())
						.Where((c) => c.OwnerID == this.ID).ToList();

					Subtract(keepObjects, removeObjects, cancellationToken, reporter);

					UiThread.RunOnIdle(() =>
					{
						rebuildLock.Dispose();
						base.Invalidate(new InvalidateArgs(this, InvalidateType.Content));
					});

					return Task.CompletedTask;
				});
		}
	}
}