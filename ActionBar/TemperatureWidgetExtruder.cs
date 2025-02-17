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
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ActionBar
{
	internal class ControlContentExtruder : FlowLayoutWidget
	{
		private int moveAmount = 10;
		private PrinterConfig printer;

		internal ControlContentExtruder(PrinterConfig printer, int extruderIndex, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.HAnchor = HAnchor.Stretch;
			this.printer = printer;

			GuiWidget macroButtons = null;
			// We do not yet support loading filament into extruders other than 0, fix it when time.
			if (extruderIndex == 0)
			{
				// add in load and unload buttons
				macroButtons = new FlowLayoutWidget()
				{
					Padding = theme.ToolbarPadding,
				};

				var loadFilament = new GCodeMacro()
				{
					GCode = AggContext.StaticData.ReadAllText(Path.Combine("SliceSettings", "load_filament.txt"))
				};

				var loadButton = new TextButton("Load".Localize(), theme)
				{
					BackgroundColor = theme.SlightShade,
					Margin = theme.ButtonSpacing,
					ToolTipText = "Load filament".Localize()
				};
				loadButton.Name = "Load Filament Button";
				loadButton.Click += (s, e) => loadFilament.Run(printer.Connection);
				macroButtons.AddChild(loadButton);

				var unloadFilament = new GCodeMacro()
				{
					GCode = AggContext.StaticData.ReadAllText(Path.Combine("SliceSettings", "unload_filament.txt"))
				};

				var unloadButton = new TextButton("Unload".Localize(), theme)
				{
					BackgroundColor = theme.SlightShade,
					Margin = theme.ButtonSpacing,
					ToolTipText = "Unload filament".Localize()
				};
				unloadButton.Click += (s, e) => unloadFilament.Run(printer.Connection);
				macroButtons.AddChild(unloadButton);

				this.AddChild(new SettingsItem("Filament".Localize(), macroButtons, theme, enforceGutter: false));
			}

			// Add the Extrude buttons
			var buttonContainer = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				Padding = theme.ToolbarPadding,
			};

			var retractButton = new TextButton("Retract".Localize(), theme)
			{
				BackgroundColor = theme.SlightShade,
				Margin = theme.ButtonSpacing,
				ToolTipText = "Retract filament".Localize()
			};
			retractButton.Click += (s, e) =>
			{
				printer.Connection.MoveExtruderRelative(moveAmount * -1, printer.Settings.EFeedRate(extruderIndex), extruderIndex);
			};
			buttonContainer.AddChild(retractButton);

			int extruderButtonTopMargin = macroButtons == null ? 8 : 0;

			var extrudeButton = new TextButton("Extrude".Localize(), theme)
			{
				BackgroundColor = theme.SlightShade,
				Margin = theme.ButtonSpacing,
				Name = "Extrude Button",
				ToolTipText = "Extrude filament".Localize()
			};
			extrudeButton.Click += (s, e) =>
			{
				printer.Connection.MoveExtruderRelative(moveAmount, printer.Settings.EFeedRate(extruderIndex), extruderIndex);
			};
			buttonContainer.AddChild(extrudeButton);

			this.AddChild(new SettingsItem(
				macroButtons == null ? "Filament".Localize() : "", // Don't put the name if we put in a macro button (it has the name)
				buttonContainer,
				theme,
				enforceGutter: false));

			var moveButtonsContainer = new FlowLayoutWidget()
			{
				VAnchor = VAnchor.Fit | VAnchor.Center,
				HAnchor = HAnchor.Fit,
				Padding = theme.ToolbarPadding,
			};

			var oneButton = theme.CreateMicroRadioButton("1");
			oneButton.CheckedStateChanged += (s, e) =>
			{
				if (oneButton.Checked)
				{
					moveAmount = 1;
				}
			};
			moveButtonsContainer.AddChild(oneButton);

			var tenButton = theme.CreateMicroRadioButton("10");
			tenButton.CheckedStateChanged += (s, e) =>
			{
				if (tenButton.Checked)
				{
					moveAmount = 10;
				}
			};
			moveButtonsContainer.AddChild(tenButton);

			var oneHundredButton = theme.CreateMicroRadioButton("100");
			oneHundredButton.CheckedStateChanged += (s, e) =>
			{
				if (oneHundredButton.Checked)
				{
					moveAmount = 100;
				}
			};
			moveButtonsContainer.AddChild(oneHundredButton);

			switch (moveAmount)
			{
				case 1:
					oneButton.Checked = true;
					break;
				case 10:
					tenButton.Checked = true;
					break;
				case 100:
					oneHundredButton.Checked = true;
					break;
			}

			moveButtonsContainer.AddChild(new TextWidget("mm", textColor: theme.Colors.PrimaryTextColor, pointSize: 8)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(3, 0)
			});

			this.AddChild(new SettingsItem("Distance".Localize(), moveButtonsContainer, theme, enforceGutter: false));
		}
	}

	internal class TemperatureWidgetHotend : TemperatureWidgetBase
	{
		private int hotendIndex = -1;

		private string sliceSettingsNote = "Note: Slice Settings are applied before the print actually starts. Changes while printing will not effect the active print.".Localize();
		private string waitingForExtruderToHeatMessage = "The extruder is currently heating and its target temperature cannot be changed until it reaches {0}°C.\n\nYou can set the starting extruder temperature in 'Slice Settings' -> 'Filament'.\n\n{1}".Localize();

		public TemperatureWidgetHotend(PrinterConfig printer, int hotendIndex, ThemeConfig theme)
			: base(printer, "150.3°", theme)
		{
			this.Name = $"Hotend {hotendIndex}";
			this.hotendIndex = hotendIndex;
			this.DisplayCurrentTemperature();
			this.ToolTipText = "Current extruder temperature".Localize();

			this.PopupContent = this.GetPopupContent(ApplicationController.Instance.MenuTheme);

			printer.Connection.HotendTemperatureRead.RegisterEvent((s, e) => DisplayCurrentTemperature(), ref unregisterEvents);
		}

		protected override int ActualTemperature => (int)printer.Connection.GetActualHotendTemperature(this.hotendIndex);
		protected override int TargetTemperature => (int)printer.Connection.GetTargetHotendTemperature(this.hotendIndex);

		private string TemperatureKey
		{
			get => "temperature" + ((this.hotendIndex > 0 && this.hotendIndex < 4) ? hotendIndex.ToString() : "");
		}

		private GuiWidget GetPopupContent(ThemeConfig theme)
		{
			var widget = new IgnoredPopupWidget()
			{
				Width = 350,
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Fit,
				BackgroundColor = theme.Colors.PrimaryBackgroundColor,
				Padding = new BorderDouble(12, 0)
			};

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			widget.AddChild(container);

			GuiWidget hotendRow;
			container.AddChild(hotendRow = new SettingsItem(
				string.Format("{0} {1}", "Hotend".Localize(), hotendIndex + 1),
				theme,
				new SettingsItem.ToggleSwitchConfig()
				{
					Checked = false,
					ToggleAction = (itemChecked) =>
					{
						if (itemChecked)
						{
							// Set to goal temp
							SetTargetTemperature(printer.Settings.Helpers.ExtruderTemperature(hotendIndex));
						}
						else
						{
							// Turn off extruder
							printer.Connection.SetTargetHotendTemperature(hotendIndex, 0);
						}
					}
				},
				enforceGutter: false));

			var toggleWidget = hotendRow.Children.Where(o => o is ICheckbox).FirstOrDefault();
			toggleWidget.Name = "Toggle Heater";

			heatToggle = toggleWidget as ICheckbox;

			int tabIndex = 0;
			var settingsContext = new SettingsContext(printer, null, NamedSettingsLayers.All);
			var settingsData = SettingsOrganizer.Instance.GetSettingsData(TemperatureKey);
			var temperatureRow = SliceSettingsTabView.CreateItemRow(settingsData, settingsContext, printer, theme, ref tabIndex);
			SliceSettingsRow.AddBordersToEditFields(temperatureRow);
			container.AddChild(temperatureRow);

			// Add the temperature row to the always enabled list ensuring the field can be set when disconnected
			alwaysEnabled.Add(temperatureRow);

			// add in the temp graph
			var graph = new DataViewGraph()
			{
				DynamiclyScaleRange = false,
				MinValue = 0,
				ShowGoal = true,
				GoalColor = ActiveTheme.Instance.PrimaryAccentColor,
				GoalValue = printer.Settings.Helpers.ExtruderTemperature(hotendIndex),
				MaxValue = 280, // could come from some profile value in the future
				Width = widget.Width - 20,
				Height = 35, // this works better if it is a common multiple of the Width
			};
			var runningInterval = UiThread.SetInterval(() =>
			{
				graph.AddData(this.ActualTemperature);
			}, 1);
			this.Closed += (s, e) => runningInterval.Continue = false;

			var valueField = temperatureRow.Descendants<MHNumberEdit>().FirstOrDefault();
			valueField.Name = "Temperature Input";
			var settingsRow = temperatureRow.DescendantsAndSelf<SliceSettingsRow>().FirstOrDefault();
			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringEvent)
				{
					if (stringEvent.Data == TemperatureKey)
					{
						var temp = printer.Settings.Helpers.ExtruderTemperature(hotendIndex);
						valueField.Value = temp;
						graph.GoalValue = temp;
						settingsRow.UpdateStyle();
						if (heatToggle.Checked)
						{
							SetTargetTemperature(temp);
						}
					}
				};
			}, ref unregisterEvents);

			container.AddChild(graph);

			if (hotendIndex == 0)
			{
				// put in the material selector
				var presetsSelector = new PresetSelectorWidget(printer, "Material".Localize(), Color.Transparent, NamedSettingsLayers.Material, theme)
				{
					Margin = new BorderDouble(right: theme.ToolbarPadding.Right),
					Padding = 0,
					BackgroundColor = Color.Transparent,
					VAnchor = VAnchor.Center | VAnchor.Fit
				};

				// Hide TextWidget
				presetsSelector.Children.First().Visible = false;

				var pulldownContainer = presetsSelector.FindNamedChildRecursive("Preset Pulldown Container");
				if (pulldownContainer != null)
				{
					pulldownContainer.Padding = theme.ToolbarPadding;
					pulldownContainer.HAnchor = HAnchor.Fit;
					pulldownContainer.Margin = 0;
					pulldownContainer.Padding = 0;
				}

				pulldownContainer.Parent.Margin = 0;

				var dropList = pulldownContainer.Children.OfType<DropDownList>().FirstOrDefault();
				if (dropList != null)
				{
					dropList.Name = "Hotend Preset Selector";
					dropList.HAnchor = HAnchor.Fit;
					dropList.Margin = 0;
				}

				container.AddChild(
					new SettingsItem("Material".Localize(), presetsSelector, theme, enforceGutter: false)
					{
						Border = new BorderDouble(0, 1)
					});
			}
			else // put in a temperature selector for the correct material
			{

			}

			// put in the actual extruder controls
			bool shareTemp = printer.Settings.GetValue<bool>(SettingsKey.extruders_share_temperature);
			int extruderCount = printer.Settings.GetValue<int>(SettingsKey.extruder_count);
			if (shareTemp && extruderCount > 1)
			{
				for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
				{
					container.AddChild(new HorizontalLine(20)
					{
						Margin = new BorderDouble(0, 5, 0, 0)
					});

					container.AddChild(new TextWidget("Extruder".Localize() + " " + (extruderIndex + 1).ToString(), pointSize: theme.DefaultFontSize)
					{
						AutoExpandBoundsToText = true,
						TextColor = Color.Black,
						HAnchor = HAnchor.Left,
					});
					container.AddChild(new ControlContentExtruder(printer, extruderIndex, theme));
				}
			}
			else
			{
				container.AddChild(new ControlContentExtruder(printer, hotendIndex, theme));
			}

			return widget;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (heatToggle != null)
			{
				heatToggle.Checked = printer.Connection.GetTargetHotendTemperature(hotendIndex) != 0;
			}

			base.OnDraw(graphics2D);
		}

		protected override void SetTargetTemperature(double targetTemp)
		{
			double goalTemp = (int)(targetTemp + .5);
			if (printer.Connection.PrinterIsPrinting
				&& printer.Connection.DetailedPrintingState == DetailedPrintingState.HeatingExtruder
				&& goalTemp != printer.Connection.GetTargetHotendTemperature(hotendIndex))
			{
				string message = string.Format(waitingForExtruderToHeatMessage, printer.Connection.GetTargetHotendTemperature(hotendIndex), sliceSettingsNote);
				StyledMessageBox.ShowMessageBox(message, "Waiting For Extruder To Heat".Localize());
			}
			else
			{
				printer.Connection.SetTargetHotendTemperature(hotendIndex, (int)(targetTemp + .5));
			}
		}
	}
}