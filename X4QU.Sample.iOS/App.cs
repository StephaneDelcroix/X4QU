//
// App.cs
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
	public class App
	{
		public static Page GetMainPage ()
		{
			return new NavigationPage(new CustomPage ());
		}
	}
}