﻿/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;
using System.Linq;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class ArrayAdvancedObject3D : Object3D
	{
		public ArrayAdvancedObject3D()
		{
			Name = "Advanced Array".Localize();
		}

		public override bool CanApply => true;

		public int Count { get; set; } = 3;

		public Vector3 Offset { get; set; } = new Vector3(30, 0, 0);

		public double Rotate { get; set; } = -15;

		public bool RotatePart { get; set; } = true;

		public double Scale { get; set; } = .9;

		public bool ScaleOffset { get; set; } = true;

		public override void Apply(UndoBuffer undoBuffer)
		{
			OperationSourceObject3D.Apply(this);

			base.Apply(undoBuffer);
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
			this.Children.Modify(list =>
			{
				IObject3D lastChild = list.First();
				list.Clear();
				list.Add(lastChild);
				var offset = Offset;
				for (int i = 1; i < Count; i++)
				{
					var rotateRadians = MathHelper.DegreesToRadians(Rotate);
					if (ScaleOffset)
					{
						offset *= Scale;
					}

					var next = lastChild.Clone();
					offset = Vector3.Transform(offset, Matrix4X4.CreateRotationZ(rotateRadians));
					next.Matrix *= Matrix4X4.CreateTranslation(offset);

					if (RotatePart)
					{
						next.Matrix = next.ApplyAtBoundsCenter(Matrix4X4.CreateRotationZ(rotateRadians));
					}

					next.Matrix = next.ApplyAtBoundsCenter(Matrix4X4.CreateScale(Scale));
					list.Add(next);
					lastChild = next;
				}
			});
		}

		public override void Remove(UndoBuffer undoBuffer)
		{
			OperationSourceObject3D.Remove(this);

			base.Remove(undoBuffer);
		}
	}
}