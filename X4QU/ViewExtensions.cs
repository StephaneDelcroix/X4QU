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
using System.Diagnostics;
using System.Collections.Generic;

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

		//FIXME: this search should probably be implemented level by level, instead of depth first
		//Check moonlight FindByName implementation, just in case 
		static View FindById (this BaseLayout view, string id)
		{
			foreach (var child in view) {
				if (child.Id == id)
					return child;
				var layoutChild = child as BaseLayout;
				if (layoutChild == null)
					continue;
				var found = FindById (layoutChild, id);
				if (found != null)
					return found;
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
			Debug.WriteLine ("HydrateElement {0} {1}", reader.Name, "-");
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
			//First, try to see if it's a BindableProperty
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