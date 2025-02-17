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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.Library.Export;

namespace MatterHackers.MatterControl
{
	public class ExportPrintItemPage : DialogPage
	{
		private CheckBox showInFolderAfterSave;

		private Dictionary<RadioButton, IExportPlugin> exportPluginButtons;

		private IEnumerable<ILibraryItem> libraryItems;

		public ExportPrintItemPage(IEnumerable<ILibraryItem> libraryItems)
		{
			this.WindowTitle = "Export File".Localize();
			this.HeaderText = "Export selection to".Localize() + ":";

			this.libraryItems = libraryItems;
			this.Name = "Export Item Window";

			var commonMargin = new BorderDouble(4, 2);

			bool isFirstItem = true;

			// TODO: Someday export operations need to resolve printer context interactively
			var printer = ApplicationController.Instance.ActivePrinter;

			// GCode export
			exportPluginButtons = new Dictionary<RadioButton, IExportPlugin>();

			foreach (IExportPlugin plugin in PluginFinder.CreateInstancesOf<IExportPlugin>().OrderBy(p => p.ButtonText))
			{
				plugin.Initialize(printer);

				// Skip plugins which are invalid for the current printer
				if (!plugin.Enabled)
				{
					continue;
				}

				// Create export button for each plugin
				var pluginButton = new RadioButton(new RadioImageWidget(plugin.ButtonText, theme.Colors.PrimaryTextColor, plugin.Icon))
				{
					HAnchor = HAnchor.Left,
					Margin = commonMargin,
					Cursor = Cursors.Hand,
					Name = plugin.ButtonText + " Button"
				};
				contentRow.AddChild(pluginButton);

				if (isFirstItem)
				{
					pluginButton.Checked = true;
					isFirstItem = false;
				}

				if (plugin is IExportWithOptions pluginWithOptions)
				{
					var optionPanel = pluginWithOptions.GetOptionsPanel();
					if (optionPanel != null)
					{
						optionPanel.HAnchor = HAnchor.Stretch;
						optionPanel.VAnchor = VAnchor.Fit;
						contentRow.AddChild(optionPanel);
					}
				}

				exportPluginButtons.Add(pluginButton, plugin);
			}

			contentRow.AddChild(new VerticalSpacer());

			// TODO: make this work on the mac and then delete this if
			if (AggContext.OperatingSystem == OSType.Windows
				|| AggContext.OperatingSystem == OSType.X11)
			{
				showInFolderAfterSave = new CheckBox("Show file in folder after save".Localize(), ActiveTheme.Instance.PrimaryTextColor, 10)
				{
					HAnchor = HAnchor.Left,
					Cursor = Cursors.Hand
				};
				contentRow.AddChild(showInFolderAfterSave);
			}

			var exportButton = theme.CreateDialogButton("Export".Localize());
			exportButton.Name = "Export Button";
			exportButton.Click += (s, e) =>
			{
				string fileTypeFilter = "";
				string targetExtension = "";

				IExportPlugin activePlugin = null;

				// Loop over all plugin buttons, break on the first checked item found
				foreach(var button in this.exportPluginButtons.Keys)
				{
					if (button.Checked)
					{
						activePlugin = exportPluginButtons[button];
						break;
					}
				}

				// Early exit if no plugin radio button is selected
				if (activePlugin == null)
				{
					return;
				}

				fileTypeFilter = activePlugin.ExtensionFilter;
				targetExtension = activePlugin.FileExtension;

				this.Parent.CloseOnIdle();

				if (activePlugin is FolderExport)
				{
					UiThread.RunOnIdle(() =>
					{
						AggContext.FileDialogs.SelectFolderDialog(
							new SelectFolderDialogParams("Select Location To Export Files")
							{
								ActionButtonLabel = "Export".Localize(),
								Title = ApplicationController.Instance.ProductName + " - " + "Select A Folder".Localize()
							},
							(openParams) =>
							{
								ApplicationController.Instance.Tasks.Execute(
									"Saving".Localize() + "...",
									async (reporter, cancellationToken) =>
									{

										string path = openParams.FolderPath;
										if (!string.IsNullOrEmpty(path))
										{
											await activePlugin.Generate(libraryItems, path, reporter, cancellationToken);
										}
									});
							});
					});

					return;
				}

				UiThread.RunOnIdle(() =>
				{
					string title = ApplicationController.Instance.ProductName + " - " + "Export File".Localize();
					string workspaceName = "Workspace " + DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss");
					AggContext.FileDialogs.SaveFileDialog(
						new SaveFileDialogParams(fileTypeFilter)
						{
							Title = title,
							ActionButtonLabel = "Export".Localize(),
							FileName = Path.GetFileNameWithoutExtension(libraryItems.FirstOrDefault()?.Name ?? workspaceName)
						},
						(saveParams) =>
						{
							string savePath = saveParams.FileName;

							if (!string.IsNullOrEmpty(savePath))
							{
								ApplicationController.Instance.Tasks.Execute(
									"Exporting".Localize() + "...",
									async (reporter, cancellationToken) =>
									{
										string extension = Path.GetExtension(savePath);
										if (extension != targetExtension)
										{
											savePath += targetExtension;
										}

										bool succeeded = false;

										if (activePlugin != null)
										{
											succeeded = await activePlugin.Generate(libraryItems, savePath, reporter, cancellationToken);
										}

										if (succeeded)
										{
											ShowFileIfRequested(savePath);
										}
										else
										{
											UiThread.RunOnIdle(() =>
											{
												StyledMessageBox.ShowMessageBox("Export failed".Localize(), title);
											});
										}
									});
							}
						});
				});
			};

			this.AddPageAction(exportButton);
		}

		private void ShowFileIfRequested(string filename)
		{
			if (AggContext.OperatingSystem == OSType.Windows || AggContext.OperatingSystem == OSType.X11)
			{
				if (showInFolderAfterSave.Checked)
				{
					AggContext.FileDialogs.ShowFileInFolder(filename);
				}
			}
		}
	}
}
