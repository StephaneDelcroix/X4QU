X4QU - Xaml for (Xamarin) QuickUI
=================================

Get Started.
------------

Say we want to create a `CustomPage` inheriting from `ContentPage`

 1. Create some xaml. 

    First, we'll create the page in xaml, like this:
 
        <ContentPage 
          xmlns="http://xamarin.com/quickui"
          xmlns:x="http://dev/null"
	      x:Class="X4QU.Sample.CustomPage">
          <ContentPage.Content>
            <StackLayout Id="Stack">
              <Label Text="Hello" Id="Label0"/>
              <Label Text="Xaml" Id="Label1">
            </StackLayout>
          </ContentPage.Content>
        </ContentPage>
 
    Some stuffs to notice:

       a. the toplevel element is <ContentPage> which is our base class
      
       b. `x:Class` is the full name of our class
  
       c. you **have** to use <ContentPage.Content> to set the Content property of the page, as there's no ContentPropertyAttribute equivalent in X.QuickUI
  
       d. collections are easier. you don't have to use <StackLayout.Children> (which won't work, as it's readonly)
       
       e. Setting an `Id` will generate a field of the same name and type accessible from code
 
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

Converters, Mode, ... are NOT YET handled

TODO
----
 - CellTemplates
 - Resources and `{StaticResource}` syntax
 - Properties with type different than string (enum, int, ...)
