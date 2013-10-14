//
// CustomPage.xaml.g.cs
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
	public partial class CustomPage
	{
		void InitializeComponent ()
		{
			this.LoadFromXaml (typeof(CustomPage));

			Stack = this.FindById<StackLayout> ("Stack");
			Text0 = this.FindById<Label> ("Label0");
			Text1 = this.FindById<Label> ("Label1");
		}

		StackLayout Stack;
		Label Text0;
		Label Text1;
	}
}

