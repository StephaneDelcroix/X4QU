using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Xamarin.QuickUI;
using Xamarin.QuickUI.Platform.Android;

namespace X4QU.Sample.Android
{
	[Activity (Label = "X4QU.Sample.Android", MainLauncher = true)]
	public class MainActivity : AndroidActivity
	{
		int count = 1;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			QuickUI.Init (this,bundle);

			var page = App.GetMainPage ();

			SetPage (page);
		}
	}
}


