//
// CustomPage.cs
//
// Author:
//       Stephane Delcroix <stephane@mi8.be>
//
// Copyright (c) 2013 Mobile Inception
//

using System;
using Xamarin.QuickUI;

namespace X4QU.Sample
{
	public partial class CustomPage : ContentPage
	{
		public CustomPage ()
		{
			InitializeComponent ();

			//Have you noticed that Binding to anonymous classes works without declaring any InternalsVisibleTo (unlike SL) ?
			BindingContext = new {
				LabelBinding = "Binding works",
				Items = new [] {
					new {Name = "Xander", Title = "Monkey"}, 
					new {Name = "John", Title = "Boss"},
					new {Name = "Fred", Title = "Pleb"}
				},
			};
		}

		public void onItemTapped (object sender, EventArg<object>  e)
		{
			Console.WriteLine ("Connecting to events in xaml works");
		}
	}
}
