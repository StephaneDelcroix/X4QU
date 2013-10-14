//
// ViewExtensions.cs
//
// Author:
//       Stephane Delcroix <stephane@mi8.be>
//
// Copyright (c) 2013 Mobile Inception
//
using System;
using Xamarin.QuickUI;
using System.Reflection;
using System.IO;
using System.Xml;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace X4QU
{
	public static class Extensions
	{
		static readonly Assembly quickUiAssembly = typeof(View).Assembly;
		static readonly string quickUiNamespace = "Xamarin.QuickUI.";
		static readonly string quickUiXmlns = "http://xamarin.com/quickui";
		static readonly string xXmlns = "http://dev/null";


		public static T FindById<T> (this BaseLayout view, string id) where T : View {
			return (T)view.FindById (id);
		}

		static View FindById (this BaseLayout view, string id)
		{
			IList<View> level0 = view.ToList ();
			while (level0.Count > 0) {
				var level1 = new List<View> ();
				foreach (var child in level0) {
					if (child.Id == id)
						return child;

					var layoutChild = child as BaseLayout;
					if (layoutChild != null)
						level1.AddRange (layoutChild);
				}
				level0 = level1;
			}
			return null;
		}

		public static void LoadFromXaml (this View view, Type callingType)
		{
			var assembly = Assembly.GetCallingAssembly ();
			var xaml = GetXamlForType (callingType, assembly);

			using (var reader = XmlReader.Create (new StringReader (xaml))) {
				while (reader.Read ()) {
					//Skip until element
					if (reader.NodeType != XmlNodeType.Element) {
						Debug.WriteLine ("Unhandled node {0} {1} {2}", reader.NodeType, reader.Name, reader.Value);
						continue;
					}
					view.HydrateElement (reader);
					break;				
				}
			}
		}

		static void HydrateElement (this object @object, XmlReader reader)
		{
			Debug.WriteLine (string.Format ("HydrateElement {0}", reader.Name));
			Debug.Assert (reader.NodeType == XmlNodeType.Element);

			//only support quickui elements for now, without namespace
			var elementType = quickUiAssembly.GetType (quickUiNamespace + reader.Name);
			Debug.Assert (@object.GetType () == elementType || @object.GetType ().IsSubclassOf (elementType));
			var elementName = reader.Name;
			var isEmpty = reader.IsEmptyElement;

			@object.SetAttributesValues (elementType, reader);

			if (isEmpty)
				return;

			while (reader.Read ()) {
				switch (reader.NodeType) {
				case XmlNodeType.EndElement:
					Debug.Assert (reader.Name == elementName); //make sure we close the right element
					return;
				case XmlNodeType.Element:
					//3 possible cases here:
					// 1. Property Element
					if (reader.Name.StartsWith (elementName + ".", StringComparison.InvariantCulture)) {
						var propertyName = reader.Name.Substring (elementName.Length + 1);
						var element = @object.ReadElement (elementType, reader);
						@object.SetPropertyValue (elementType, propertyName, element);
					}
					// 2. implicit Content Property (not supported right now)
					else if (false) {

					}
					// 3. collection syntax
					else if (elementType.IsSubclassOfRawGeneric (typeof(Layout<>))) {
						var layout = (Layout)@object;
						Debug.Assert (layout != null);
						var element = quickUiAssembly.GetType (quickUiNamespace + reader.Name).GetConstructor (new Type[]{ }).Invoke (new object[]{ });
						Debug.Assert (element != null);
						element.HydrateElement (reader);
						Debug.Assert (element.GetType ().IsSubclassOf (typeof(View)));
						layout.Add ((View)element);
					}
					break;
				case XmlNodeType.Whitespace:
					break;
				default:
					Debug.WriteLine ("Unhandled node {0} {1} {2}", reader.NodeType, reader.Name, reader.Value);
					break;
				}
			}
		}

		static void SetAttributesValues (this object @object, Type elementType, XmlReader reader)
		{
			Debug.Assert (reader.NodeType == XmlNodeType.Element);
			for (var i = 0; i < reader.AttributeCount; i++) {
				reader.MoveToAttribute (i);
				Debug.WriteLine ("Attribute {0} {1} {2}", reader.NamespaceURI, reader.Name, reader.Value);

				//skip xmlns for now
				if (reader.NamespaceURI == "http://www.w3.org/2000/xmlns/")
					continue;
				//skip x: attributes
				if (reader.NamespaceURI == xXmlns)
					continue;

				@object.SetPropertyValue (elementType, reader.Name, reader.Value);
			}
		}

		static void SetPropertyValue (this object @object, Type elementType, string propertyName, object @value)
		{
			//If it's a binding string: special handling
			var valueString = @value as string;
			if (valueString != null && valueString.Trim ().StartsWith ("{", StringComparison.InvariantCulture)) {
				@object.SetBinding (elementType, propertyName, valueString);
				return;
			}

			//try to see if it's a BindableProperty
			var bindableFieldInfo = 
				elementType.GetField (propertyName + "Property", 
					BindingFlags.Static | 
					BindingFlags.Public | 
					BindingFlags.FlattenHierarchy);
			
			if (elementType.IsSubclassOf (typeof(BindableObject)) && bindableFieldInfo != null) {
				var property = bindableFieldInfo.GetValue (null) as BindableProperty;
				((BindableObject)@object).SetValue (property, @value);
				return;
			}

			//Fallback to normal properties
			var propertyInfo = elementType.GetProperty (propertyName);
			if (propertyInfo != null) {
				var setter = propertyInfo.SetMethod;
				setter.Invoke (@object, new [] { @value });
				return;
			}
			throw new Exception (String.Format ("Xaml Parse issue. No Property of name {0} found", propertyName));
		}

		static void SetBinding (this object @object, Type elementType, string propertyName, string bindingString)
		{
			var bindableFieldInfo = 
				elementType.GetField (propertyName + "Property", 
					BindingFlags.Static | 
					BindingFlags.Public | 
					BindingFlags.FlattenHierarchy);
			if (bindableFieldInfo == null || !elementType.IsSubclassOf (typeof(BindableObject))) {
				Debug.Fail ("Invalid Binding");
				return;
			}
			var property = bindableFieldInfo.GetValue (null) as BindableProperty;

			if (!bindingString.StartsWith ("{", StringComparison.InvariantCulture) 
				&& !bindingString.EndsWith ("}", StringComparison.InvariantCulture)) {
				Debug.Fail ("Invalid Binding");
				return;			
			}

			bindingString = bindingString.Substring (1, bindingString.Length - 2);
			//get the path
			string path = null;
			if (string.IsNullOrEmpty(path)) {
				var regex = new Regex (@"Binding +Path *= *(\w+\b)");
				var match = regex.Match (bindingString);
				if (match != null) {
					path = match.Groups [1].Value;
				}
			}
			if (string.IsNullOrEmpty(path)) { 
				var regex = new Regex (@"Binding +(\w+\b)");
				var match = regex.Match (bindingString);
				if (match != null)
					path = match.Groups [1].Value;
			}

			var binding = new Binding (path);
			((BindableObject)@object).SetBinding (property, binding);

		}

		static object ReadElement (this object @object, Type elementType, XmlReader reader)
		{
 			Debug.Assert (reader.NodeType == XmlNodeType.Element);
			var nodeName = reader.Name;
			object element;
			while (reader.Read ()) {
				switch (reader.NodeType) {
				case XmlNodeType.Element:
					element = quickUiAssembly.GetType (quickUiNamespace + reader.Name).GetConstructor (new Type[]{ }).Invoke (new object[]{ });
					Debug.Assert (element != null);
					element.HydrateElement (reader);
					if (reader.IsEmptyElement)
						return element;
					break;
				case XmlNodeType.EndElement:
					Debug.Assert (reader.Name == nodeName);
					return element;
				case XmlNodeType.Text:
					element = reader.Value.Trim();
				case XmlNodeType.Whitespace:
					break;
				default:
					Debug.WriteLine ("Unhandled node {0} {1} {2}", reader.NodeType, reader.Name, reader.Value);
					break;
				}
			}
			throw new Exception ("Xaml Parse issue: closing PropertyElement expected)");
		}

		//FIXME: use AT LEAST a regex, lazy. Or anything stronger than this crap
		static string GetXamlForType (Type type, Assembly assembly)
		{
			foreach (var resource in assembly.GetManifestResourceNames()) {
				using (var stream = assembly.GetManifestResourceStream (resource))
				using (var reader = new StreamReader (stream)) {
					var xaml = reader.ReadToEnd ();
					if (xaml.Contains (String.Format ("x:Class=\"{0}\"", type.FullName)))
						return xaml;
				}
			}
			return null;
		}

		static bool IsSubclassOfRawGeneric(this Type toCheck, Type generic) {
			while (toCheck != null && toCheck != typeof(object)) {
				var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
				if (generic == cur) {
					return true;
				}
				toCheck = toCheck.BaseType;
			}
			return false;
		}
	}
}