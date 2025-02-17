﻿using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using NUnit.Framework;

namespace MatterHackers.MatterControl.Tests.Automation
{
	[TestFixture, Category("Agg.UI.Automation"), Apartment(ApartmentState.STA), RunInApplicationDomain]
	public class ShowTerminalButtonClickedOpensTerminal
	{
		[Test]
		public async Task ClickingShowTerminalButtonOpensTerminal()
		{
			await MatterControlUtilities.RunTest((testRunner) =>
			{
				testRunner.AddAndSelectPrinter("Airwolf 3D", "HD");

				Assert.IsFalse(testRunner.WaitForName("TerminalWidget", 0.5), "Terminal Window should not exist");

				testRunner.ClickByName("Terminal Sidebar");
				testRunner.Delay(1);

				Assert.IsTrue(testRunner.WaitForName("TerminalWidget"), "Terminal Window should exists after Show Terminal button is clicked");

				return Task.CompletedTask;
			});
		}
	}
}
