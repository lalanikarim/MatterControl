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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	using Aabb = AxisAlignedBoundingBox;

	[JsonConverter(typeof(StringEnumConverter))]
	public enum Align { None, Min, Center, Max, Origin }

	[JsonConverter(typeof(StringEnumConverter))]
	public enum Alignment { X, Y, Z, negX, negY, negZ };

	[JsonConverter(typeof(StringEnumConverter))]
	[Flags]
	public enum Edge
	{
		LeftFront = FaceAlign.Left | FaceAlign.Front,
		LeftBack = FaceAlign.Left | FaceAlign.Back,
		LeftBottom = FaceAlign.Left | FaceAlign.Bottom,
		LeftTop = FaceAlign.Left | FaceAlign.Top,
		RightFront = FaceAlign.Right | FaceAlign.Front,
		RightBack = FaceAlign.Right | FaceAlign.Back,
		RightBottom = FaceAlign.Right | FaceAlign.Bottom,
		RightTop = FaceAlign.Right | FaceAlign.Top,
		FrontBottom = FaceAlign.Front | FaceAlign.Bottom,
		FrontTop = FaceAlign.Front | FaceAlign.Top,
		BackBottom = FaceAlign.Back | FaceAlign.Bottom,
		BackTop = FaceAlign.Back | FaceAlign.Top
	}

	[JsonConverter(typeof(StringEnumConverter))]
	[Flags]
	public enum FaceAlign
	{
		Left = 0x01,
		Right = 0x02,
		Front = 0x04,
		Back = 0x08,
		Bottom = 0x10,
		Top = 0x20,
	};

	[JsonConverter(typeof(StringEnumConverter))]
	[Flags]
	public enum Side2D
	{
		Left = 0x01,
		Right = 0x02,
		Bottom = 0x10,
		Top = 0x20,
	};

	public class AlignObject3D : Object3D, IPropertyGridModifier
	{
		// We need to serialize this so we can remove the arrange and get back to the objects before arranging
		public List<Aabb> OriginalChildrenBounds = new List<Aabb>();

		public AlignObject3D()
		{
			Name = "Align";
		}

		public AlignObject3D(IObject3D objectToAlign, FaceAlign boundingFacesToAlign, IObject3D objectToAlignTo, FaceAlign boundingFacesToAlignTo, double offsetX = 0, double offsetY = 0, double offsetZ = 0, string name = "")
			: this(objectToAlign, boundingFacesToAlign, GetPositionToAlignTo(objectToAlignTo, boundingFacesToAlignTo, new Vector3(offsetX, offsetY, offsetZ)), name)
		{
			if (objectToAlign == objectToAlignTo)
			{
				throw new Exception("You cannot align an object to itself.");
			}
		}

		public AlignObject3D(IObject3D objectToAlign, FaceAlign boundingFacesToAlign, double positionToAlignToX = 0, double positionToAlignToY = 0, double positionToAlignToZ = 0, string name = "")
			: this(objectToAlign, boundingFacesToAlign, new Vector3(positionToAlignToX, positionToAlignToY, positionToAlignToZ), name)
		{
		}

		public AlignObject3D(IObject3D objectToAlign, FaceAlign boundingFacesToAlign, Vector3 positionToAlignTo, double offsetX, double offsetY, double offsetZ, string name = "")
			: this(objectToAlign, boundingFacesToAlign, positionToAlignTo + new Vector3(offsetX, offsetY, offsetZ), name)
		{
		}

		public AlignObject3D(IObject3D item, FaceAlign boundingFacesToAlign, Vector3 positionToAlignTo, string name = "")
		{
			AxisAlignedBoundingBox bounds = item.GetAxisAlignedBoundingBox();

			if (IsSet(boundingFacesToAlign, FaceAlign.Left, FaceAlign.Right))
			{
				positionToAlignTo.X = positionToAlignTo.X - bounds.minXYZ.X;
			}
			if (IsSet(boundingFacesToAlign, FaceAlign.Right, FaceAlign.Left))
			{
				positionToAlignTo.X = positionToAlignTo.X - bounds.minXYZ.X - (bounds.maxXYZ.X - bounds.minXYZ.X);
			}
			if (IsSet(boundingFacesToAlign, FaceAlign.Front, FaceAlign.Back))
			{
				positionToAlignTo.Y = positionToAlignTo.Y - bounds.minXYZ.Y;
			}
			if (IsSet(boundingFacesToAlign, FaceAlign.Back, FaceAlign.Front))
			{
				positionToAlignTo.Y = positionToAlignTo.Y - bounds.minXYZ.Y - (bounds.maxXYZ.Y - bounds.minXYZ.Y);
			}
			if (IsSet(boundingFacesToAlign, FaceAlign.Bottom, FaceAlign.Top))
			{
				positionToAlignTo.Z = positionToAlignTo.Z - bounds.minXYZ.Z;
			}
			if (IsSet(boundingFacesToAlign, FaceAlign.Top, FaceAlign.Bottom))
			{
				positionToAlignTo.Z = positionToAlignTo.Z - bounds.minXYZ.Z - (bounds.maxXYZ.Z - bounds.minXYZ.Z);
			}

			Matrix *= Matrix4X4.CreateTranslation(positionToAlignTo);
			Children.Add(item.Clone());
		}

		[ShowAsList]
		[DisplayName("Anchor")]
		public ChildrenSelector AnchorObjectSelector { get; set; } = new ChildrenSelector();

		public bool Advanced { get; set; } = false;

		[DisplayName("X")]
		[Icons(new string[] { "424.png", "align_left.png", "align_center_x.png", "align_right.png", "align_origin.png" })]
		public Align XAlign { get; set; } = Align.None;

		[DisplayName("Start X")]
		[Icons(new string[] { "424.png", "align_to_left.png", "align_to_center_x.png", "align_to_right.png", "" })]
		public Align XAlignTo { get; set; } = Align.None;

		[DisplayName("Offset X")]
		public double XOffset { get; set; } = 0;

		[DisplayName("Y")]
		[Icons(new string[] { "424.png", "align_bottom.png", "align_center_y.png", "align_Top.png", "align_origin.png" })]
		public Align YAlign { get; set; } = Align.None;

		[DisplayName("Start Y")]
		[Icons(new string[] { "424.png", "align_to_bottom.png", "align_to_center_y.png", "align_to_top.png", "" })]
		public Align YAlignTo { get; set; } = Align.None;

		[DisplayName("Offset Y")]
		public double YOffset { get; set; } = 0;

		[DisplayName("Z")]
		[Icons(new string[] { "424.png", "align_bottom.png", "align_center_y.png", "align_Top.png", "align_origin.png" })]
		public Align ZAlign { get; set; } = Align.None;

		[DisplayName("Start Z")]
		[Icons(new string[] { "424.png", "align_to_bottom.png", "align_to_center_y.png", "align_to_top.png", "" })]
		public Align ZAlignTo { get; set; } = Align.None;

		[DisplayName("Offset Z")]
		public double ZOffset { get; set; } = 0;

		public override bool CanApply => true;

		private List<Aabb> CurrentChildrenBounds
		{
			get
			{
				var currentChildrenBounds = new List<Aabb>();
				this.Children.Modify(list =>
				{
					foreach (var child in list)
					{
						currentChildrenBounds.Add(child.GetAxisAlignedBoundingBox());
					}
				});

				return currentChildrenBounds;
			}
		}

		[JsonIgnore]
		private IObject3D AnchorObject
		{
			get
			{
				if (AnchorObjectSelector.Count == 1)
				{
					return this.Children.Where(c => c.ID == AnchorObjectSelector[0]).FirstOrDefault();
				}

				return null;
			}
		}

		[JsonIgnore]
		private int AnchorObjectIndex
		{
			get
			{
				int index = 0;
				foreach(var child in this.Children)
				{ 
					if(child.ID == AnchorObjectSelector[0])
					{
						return index;
					}
					index++;
				}

				return -1;
			}
		}

		public static Vector3 GetPositionToAlignTo(IObject3D objectToAlignTo, FaceAlign boundingFacesToAlignTo, Vector3 extraOffset)
		{
			Vector3 positionToAlignTo = new Vector3();
			if (IsSet(boundingFacesToAlignTo, FaceAlign.Left, FaceAlign.Right))
			{
				positionToAlignTo.X = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.X;
			}
			if (IsSet(boundingFacesToAlignTo, FaceAlign.Right, FaceAlign.Left))
			{
				positionToAlignTo.X = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.X;
			}
			if (IsSet(boundingFacesToAlignTo, FaceAlign.Front, FaceAlign.Back))
			{
				positionToAlignTo.Y = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.Y;
			}
			if (IsSet(boundingFacesToAlignTo, FaceAlign.Back, FaceAlign.Front))
			{
				positionToAlignTo.Y = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.Y;
			}
			if (IsSet(boundingFacesToAlignTo, FaceAlign.Bottom, FaceAlign.Top))
			{
				positionToAlignTo.Z = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.Z;
			}
			if (IsSet(boundingFacesToAlignTo, FaceAlign.Top, FaceAlign.Bottom))
			{
				positionToAlignTo.Z = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.Z;
			}
			return positionToAlignTo + extraOffset;
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

			var childrenIds = Children.Select(c => c.ID).ToArray();
			if(childrenIds.Length == 0)
			{
				AnchorObjectSelector.Clear();
			}
			else if (AnchorObjectSelector.Count != 1
				|| !AnchorObjectSelector.Where(i => childrenIds.Contains(i)).Any())
			{
				AnchorObjectSelector.Clear();
				AnchorObjectSelector.Add(childrenIds[0]);
			}

			// if the count of our children changed clear our cache of the bounds
			if (Children.Count != OriginalChildrenBounds.Count)
			{
				OriginalChildrenBounds.Clear();
			}

			using (RebuildLock())
			{
				var aabb = this.GetAxisAlignedBoundingBox();

				// TODO: check if the has code for the children
				if (OriginalChildrenBounds.Count == 0)
				{
					this.Children.Modify(list =>
					{
						foreach (var child in list)
						{
							OriginalChildrenBounds.Add(child.GetAxisAlignedBoundingBox());
						}
					});
				}

				this.Children.Modify(list =>
				{
					if (list.Count == 0)
					{
						return;
					}
					int anchorIndex = AnchorObjectIndex;
					var anchorBounds = CurrentChildrenBounds[anchorIndex];

					int i = 0;
					// first align the anchor object
					foreach (var child in list)
					{
						if (XAlign == Align.None
							|| i == anchorIndex)
						{
							if (i < OriginalChildrenBounds.Count)
							{
								// make sure it is where it started
								AlignAxis(0, Align.Min, OriginalChildrenBounds[i].minXYZ.X, 0, child);
							}
						}

						if (YAlign == Align.None
							|| i == anchorIndex)
						{
							if (i < OriginalChildrenBounds.Count)
							{
								AlignAxis(1, Align.Min, OriginalChildrenBounds[i].minXYZ.Y, 0, child);
							}
						}

						if (ZAlign == Align.None
							|| i == anchorIndex)
						{
							if (i < OriginalChildrenBounds.Count)
							{
								AlignAxis(2, Align.Min, OriginalChildrenBounds[i].minXYZ.Z, 0, child);
							}
						}
						i++;
					}

					// the align all the objects to it
					i = 0;
					foreach (var child in list)
					{
						if (XAlign != Align.None
							&& i != anchorIndex)
						{
							if (XAlign == Align.Origin)
							{
								// find the origin in world space of the child
								var firstOrigin = Vector3.Transform(Vector3.Zero, AnchorObject.WorldMatrix());
								var childOrigin = Vector3.Transform(Vector3.Zero, child.WorldMatrix());
								child.Translate(new Vector3(-(childOrigin - firstOrigin).X + (Advanced ? XOffset : 0), 0, 0));
							}
							else
							{
								AlignAxis(0, XAlign, GetAlignToOffset(CurrentChildrenBounds, 0, (!Advanced || XAlignTo == Align.None) ? XAlign : XAlignTo), XOffset, child);
							}
						}

						if (YAlign != Align.None
							&& i != anchorIndex)
						{
							if (YAlign == Align.Origin)
							{
								// find the origin in world space of the child
								var firstOrigin = Vector3.Transform(Vector3.Zero, AnchorObject.WorldMatrix());
								var childOrigin = Vector3.Transform(Vector3.Zero, child.WorldMatrix());
								child.Translate(new Vector3(0, -(childOrigin - firstOrigin).Y + (Advanced ? YOffset : 0), 0));
							}
							else
							{
								AlignAxis(1, YAlign, GetAlignToOffset(CurrentChildrenBounds, 1, (!Advanced || YAlignTo == Align.None) ? YAlign : YAlignTo), YOffset, child);
							}
						}

						if (ZAlign != Align.None
							&& i != anchorIndex)
						{
							if (ZAlign == Align.Origin)
							{
								// find the origin in world space of the child
								var firstOrigin = Vector3.Transform(Vector3.Zero, AnchorObject.WorldMatrix());
								var childOrigin = Vector3.Transform(Vector3.Zero, child.WorldMatrix());
								child.Translate(new Vector3(0, 0, -(childOrigin - firstOrigin).Z + (Advanced ? ZOffset : 0)));
							}
							else
							{
								AlignAxis(2, ZAlign, GetAlignToOffset(CurrentChildrenBounds, 2, (!Advanced || ZAlignTo == Align.None) ? ZAlign : ZAlignTo), ZOffset, child);
							}
						}
						i++;
					}
				});
			}

			Invalidate(new InvalidateArgs(this, InvalidateType.Matrix, null));
		}

		public override void Remove(UndoBuffer undoBuffer)
		{
			using (RebuildLock())
			{
				// put everything back to where it was before the arrange started
				if (OriginalChildrenBounds.Count == Children.Count)
				{
					int i = 0;
					foreach (var child in Children)
					{
						// Where you are minus where you started to get back to where you started
						child.Translate(-(child.GetAxisAlignedBoundingBox().minXYZ - OriginalChildrenBounds[i].minXYZ));
						i++;
					}
				}

				base.Remove(undoBuffer);
			}

			Invalidate(new InvalidateArgs(this, InvalidateType.Content));
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			var editRow = change.Context.GetEditRow(nameof(XAlignTo));
			if (editRow != null) editRow.Visible = Advanced && XAlign != Align.Origin;
			editRow = change.Context.GetEditRow(nameof(XOffset));
			if (editRow != null) editRow.Visible = Advanced;
			editRow = change.Context.GetEditRow(nameof(YAlignTo));
			if (editRow != null) editRow.Visible = Advanced && YAlign != Align.Origin;
			editRow = change.Context.GetEditRow(nameof(YOffset));
			if (editRow != null) editRow.Visible = Advanced;
			editRow = change.Context.GetEditRow(nameof(ZAlignTo));
			if (editRow != null) editRow.Visible = Advanced && ZAlign != Align.Origin;
			editRow = change.Context.GetEditRow(nameof(ZOffset));
			if (editRow != null) editRow.Visible = Advanced;
		}

		private static bool IsSet(FaceAlign variableToCheck, FaceAlign faceToCheckFor, FaceAlign faceToAssertNot)
		{
			if ((variableToCheck & faceToCheckFor) != 0)
			{
				if ((variableToCheck & faceToAssertNot) != 0)
				{
					throw new Exception("You cannot have both " + faceToCheckFor.ToString() + " and " + faceToAssertNot.ToString() + " set when calling Align.  The are mutually exclusive.");
				}
				return true;
			}

			return false;
		}

		private void AlignAxis(int axis, Align align, double alignTo, double offset, IObject3D item)
		{
			var aabb = item.GetAxisAlignedBoundingBox();
			var translate = Vector3.Zero;

			switch (align)
			{
				case Align.Min:
					translate[axis] = alignTo - aabb.minXYZ[axis] + offset;
					break;

				case Align.Center:
					translate[axis] = alignTo - aabb.Center[axis] + offset;
					break;

				case Align.Max:
					translate[axis] = alignTo - aabb.maxXYZ[axis] + offset;
					break;
			}

			item.Translate(translate);
		}

		private double GetAlignToOffset(List<Aabb> currentChildrenBounds, int axis, Align alignTo)
		{
			switch (alignTo)
			{
				case Align.Min:
					return currentChildrenBounds[AnchorObjectIndex].minXYZ[axis];

				case Align.Center:
					return currentChildrenBounds[AnchorObjectIndex].Center[axis];

				case Align.Max:
					return currentChildrenBounds[AnchorObjectIndex].maxXYZ[axis];

				default:
					throw new NotImplementedException();
			}
		}
	}
}