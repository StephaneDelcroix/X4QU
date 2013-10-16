X4QU - Xaml for (Xamarin) QuickUI
=================================

Get Started.
------------

Say we want to create a `CustomPage` inheriting from `ContentPage`

 1. Create some xaml. 

    First, we'll create the page in xaml, like this:
 
        <ContentPage 
          xmlns="http://xamarin.com/quickui"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          x:Class="X4QU.Sample.CustomPage">
          <ContentPage.Content>
            <StackLayout Id="Stack" Orientation="Vertical">
              <Label Text="Hello" Id="Label0"/>
              <Label Text="Xaml" Id="Label1">
            </StackLayout>
          </ContentPage.Content>
        </ContentPage>
 
    Some stuffs to notice:
    
     1. the toplevel element is <ContentPage> which is our base class
     2. `x:Class` is the full name of our class
     3. you **have** to use <ContentPage.Content> to set the Content property of the page, as there's no ContentPropertyAttribute equivalent in X.QuickUI
     4. collections are easy. you don't have to use `<StackLayout.Children>` (which won't work, as it's readonly)
     5. Setting an `Id` will generate a field of the same name and type accessible from code
 
    Add this file to your project as `EmbeddedResource`
 
 2. Create the class

    Create your class skeleton
 
        using System;
        using Xamarin.QuickUI;

        namespace X4QU.Sample
        {
	        public partial class CustomPage : ContentPage
	        {
		        public CustomPage ()
		        {
			        InitializeComponent ();
			    }
			}
        }
    
     The name of this class is irrelevant, by I name if CutomPage.xaml.cs

 3. Generate the xaml.g.cs
 
    Use `mono xamlg.exe CustomPage.xaml` to generate `CustomPage.xaml.g.cs`
 
And you're all set

Bindings
--------
Those syntaxes are supported:

    <Label Text="{Binding MyPropertyPath}"/>
    <Label Text="{Binding Path=MyPropertyPath}"/>

This syntax is NOT supported (as the xaml parser require a parameterless ctor, and Binding doesn't have one.

    <Label>
      <Label.Text>
        <Binding Path="MyPropertyPath">
      </Label.Text>
    </Label>

`Converter`, `Mode`, ... are NOT YET handled

ListView, Cells and Templates
-----------------------------

    <ContentPage 
        xmlns="http://xamarin.com/quickui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:X4QU="clr-namespace:X4QU.Extensions;assembly=X4QU"
        x:Class="X4QU.Sample.CustomPage">
      <ContentPage.Content>
        <ListView ItemSource="{Binding Items}">
          <ListView.Template>
            <X4QU:TextCellTemplate TextCell.Text="{Binding Name}" TextCell.Detail="{Binding Title}" />
          </ListView.Template>
        </ListView>
      </ContentPage.Content>
    </ContentPage>

Note: there's a new assembly to reference, declaring a TextCellTemplate class, which is only a `CellTemplate(typeof(TextCell))`

TODO
----
 - Resources and `{StaticResource}` syntax
 - Properties with type different than string and enums (double, int, ...)

Features requests in Xamarin.QuickUI
------------------------------------
 - TypeConverterAttribute
 - ContentPropertyAttribute
