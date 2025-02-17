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

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public abstract class TemperatureStatusWidget : FlowLayoutWidget
	{
		protected TextWidget actualTemp;
		protected ProgressBar progressBar;
		protected TextWidget targetTemp;
		protected EventHandler unregisterEvents;
		private int fontSize = 14;
		protected PrinterConfig printer;

		public TemperatureStatusWidget(PrinterConfig printer, string dispalyName)
		{
			this.printer = printer;
			var extruderName = new TextWidget(dispalyName, pointSize: fontSize, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(right: 8)
			};

			this.AddChild(extruderName);

			progressBar = new ProgressBar(200, 6)
			{
				FillColor = ActiveTheme.Instance.PrimaryAccentColor,
				Margin = new BorderDouble(right: 10),
				BorderColor = Color.Transparent,
				BackgroundColor = new Color(ActiveTheme.Instance.PrimaryTextColor, 50),
				VAnchor = VAnchor.Center,
			};
			this.AddChild(progressBar);

			actualTemp = new TextWidget("", pointSize: fontSize, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(right: 0),
				Width = 60
			};
			this.AddChild(actualTemp);

			this.AddChild(new VerticalLine()
			{
				BackgroundColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(8, 0)
			});

			targetTemp = new TextWidget("", pointSize: fontSize, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(right: 8),
				Width = 60
			};
			this.AddChild(targetTemp);

			UiThread.RunOnIdle(UpdateTemperatures);
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
		}

		public abstract void UpdateTemperatures();
	}
}