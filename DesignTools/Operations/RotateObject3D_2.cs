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
using MatterHackers.MatterControl.DesignTools.EditableTypes;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Linq;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class RotateObject3D_2 : TransformWrapperObject3D, IEditorDraw
	{
		public RotateObject3D_2()
		{
			Name = "Rotate".Localize();
		}

		public RotateObject3D_2(IObject3D itemToRotate, double xRadians = 0, double yRadians = 0, double zRadians = 0, string name = "")
			: this()
		{
			var aabb = itemToRotate.GetAxisAlignedBoundingBox();

			this.RotateAbout.Origin = aabb.Center;

			var rotateItem = new Object3D();
			this.Children.Add(rotateItem);
			rotateItem.Children.Add(itemToRotate);

			Rebuild(null);
		}

		public RotateObject3D_2(IObject3D itemToRotate, Vector3 translation, string name = "")
			: this(itemToRotate, translation.X, translation.Y, translation.Z, name)
		{
		}

		public override bool CanApply => true;

		#region // editable properties
		public DirectionAxis RotateAbout { get; set; } = new DirectionAxis() { Origin = Vector3.NegativeInfinity, Normal = Vector3.UnitZ };
		[DisplayName("Angle")]
		public double AngleDegrees { get; set; } = 0;
		#endregion

		[JsonIgnore]
		public Matrix4X4 RotationMatrix
		{
			get
			{
				var angleRadians = MathHelper.DegreesToRadians(AngleDegrees);
				var rotation = Matrix4X4.CreateTranslation(-RotateAbout.Origin)
					* Matrix4X4.CreateRotation(RotateAbout.Normal, angleRadians)
					* Matrix4X4.CreateTranslation(RotateAbout.Origin);

				return rotation;
			}
		}

		public static RotateObject3D_2 Create(IObject3D itemToRotate)
		{
			return new RotateObject3D_2(itemToRotate);
		}

		public void DrawEditor(object sender, DrawEventArgs e)
		{
			if (sender is InteractionLayer layer
				&& layer.Scene.SelectedItem != null
				&& layer.Scene.SelectedItem.DescendantsAndSelf().Where((i) => i == this).Any())
			{
				layer.World.RenderDirectionAxis(RotateAbout, this.WorldMatrix(), 30);
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

			using (RebuildLock())
			{
				// set the matrix for the inner object
				TransformItem.Matrix = RotationMatrix;
			}

			Invalidate(new InvalidateArgs(this, InvalidateType.Matrix, null));
		}
	}
}