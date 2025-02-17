﻿/*
Copyright (c) 2017, Kevin Pope, John Lewin
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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.Library
{
	public class InsertionGroupObject3D : Object3D
	{
		public event EventHandler ContentLoaded;

		internal static Mesh placeHolderMesh;

		private InteractiveScene scene;
		private View3DWidget view3DWidget;

		public Task LoadingItemsTask { get; }

		static InsertionGroupObject3D()
		{
			// Create the placeholder mesh and position it at z0
			placeHolderMesh = PlatonicSolids.CreateCube(20, 20, 20);
			placeHolderMesh.Translate(new Vector3(0, 0, 10));
		}

		// TODO: Figure out how best to collapse the InsertionGroup after the load task completes
		public InsertionGroupObject3D(IEnumerable<ILibraryItem> items, View3DWidget view3DWidget, InteractiveScene scene, Vector2 bedCenter, Func<bool> dragOperationActive, bool trackSourceFiles = false)
		{
			if(items == null)
			{
				return;
			}

			// Add a temporary placeholder to give us some bounds
			this.scene = scene;
			this.view3DWidget = view3DWidget;

			this.LoadingItemsTask = Task.Run((Func<Task>)(async () =>
			{
				var newItemOffset = Vector2.Zero;
				if (dragOperationActive != null
					&& !dragOperationActive())
				{
					newItemOffset = bedCenter;
				}

				var offset = Matrix4X4.Identity;

				// Add the placeholder 'Loading...' object
				var placeholderItem = new Object3D()
				{
					Mesh = placeHolderMesh,
					Matrix = Matrix4X4.Identity,
					Parent = this
				};

				this.Children.Add(placeholderItem);

				// Filter to content file types only
				foreach (var item in items.Where(item => item.IsContentFileType()).ToList())
				{

					// Acquire
					var progressControl = new DragDropLoadProgress(view3DWidget, null);

					// Position at accumulating offset
					placeholderItem.Matrix *= Matrix4X4.CreateTranslation(newItemOffset.X, (double)newItemOffset.Y, 0);
					placeholderItem.Visible = true;
					progressControl.TrackingObject = placeholderItem;

					var loadedItem = await item.CreateContent(progressControl.ProgressReporter);
					if (loadedItem != null)
					{
						var aabb = loadedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

						// lets move the cube to the center of the loaded thing
						placeholderItem.Matrix *= Matrix4X4.CreateTranslation(-10 + aabb.XSize/2, 0, 0);

						placeholderItem.Visible = false;

						// Copy scale/rotation/translation from the source and Center
						loadedItem.Matrix = loadedItem.Matrix * Matrix4X4.CreateTranslation((double)-aabb.Center.X, (double)-aabb.Center.Y, (double)-aabb.minXYZ.Z) * placeholderItem.Matrix;

						// check if the item has 0 height (it is probably an image)
						if(loadedItem.ZSize() == 0)
						{
							// raise it up a bit so it is not z fighting with the bed
							loadedItem.Matrix *= Matrix4X4.CreateTranslation(0, 0, .1);
						}

						loadedItem.Color = loadedItem.Color;

						// Set mesh path if tracking requested
						if (trackSourceFiles 
							&& item is FileSystemFileItem fileItem
							&& item.IsMeshFileType())
						{
							loadedItem.MeshPath = fileItem.Path;
						}

						// Notification should force invalidate and redraw
						//progressReporter?.Invoke(1, "");

						this.Children.Add(loadedItem);

						loadedItem.MakeNameNonColliding();

						// Wait for content to load

						// Adjust next item position
						// TODO: do something more interesting than increment in x
						newItemOffset.X = loadedItem.GetAxisAlignedBoundingBox(Matrix4X4.Identity).XSize/2 + 10;
					}

					progressControl.ProgressReporter(1.3, "");
				}

				this.Children.Remove(placeholderItem);

				this.ContentAcquired = true;

				ContentLoaded?.Invoke(this, null);

				if (dragOperationActive != null
					&& !dragOperationActive())
				{
					this.Collapse();
				}

				this.Invalidate(new InvalidateArgs(this, InvalidateType.Content));
			}));
		}

		/// <summary>
		/// Indicates if all content has been acquired or if pending operations are still active
		/// </summary>
		public bool ContentAcquired { get; set; } = false;

		/// <summary>
		/// Collapse the InsertionGroup into the scene
		/// </summary>
		public void Collapse()
		{
			// Drag operation has finished, we need to perform the collapse
			var loadedItems = this.Children;

			if(loadedItems.Count == 1)
			{
				var first = loadedItems.First();
				if (first.GetType() == typeof(Object3D)
					&& first.Mesh == null
					&& first.Children.Count == 1)
				{
					// collapse our first child into this
					this.Children.Modify(list =>
					{
						first.CollapseInto(list, false);
					});
				}
			}

			// Collapse our contents into the root of the scene
			// of the scene when it loses focus
			scene.Children.Modify(list =>
			{
				this.CollapseInto(list, false);
			});

			// Create and push the undo operation
			foreach (var item in loadedItems)
			{
				view3DWidget.AddUndoOperation(new InsertCommand(scene, item));
			}

			if (scene.SelectedItem == this
				&& loadedItems.Count > 0)
			{
				scene.ClearSelection();

				foreach (var item in loadedItems)
				{
					scene.AddToSelection(item);
				}
			}
		}
	}
}
