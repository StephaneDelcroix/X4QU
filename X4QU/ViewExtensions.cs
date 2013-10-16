//
// ViewExtensions.cs
//
// Author:
//       Stephane Delcroix <stephane@mi8.be>
//
// Copyright (c) 2013 Mobile Inception
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Reflection;
using System.IO;
using System.Xml;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Xamarin.QuickUI;

namespace X4QU
{
	public static class Extensions
	{
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

			var elementType = GetElementType (reader.NamespaceURI, reader.Name);
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
						var element = GetElementType (reader.NamespaceURI, reader.Name).Create ();
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
				if (reader.NamespaceURI == "http://schemas.microsoft.com/winfx/2006/xaml")
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
				//TypeConverters, where are you ?
				((BindableObject)@object).SetValue (property, @value);
				return;
			}

			//Fallback to normal properties
			var propertyInfo = elementType.GetProperty (propertyName);
			if (propertyInfo != null) {
				var setter = propertyInfo.SetMethod;
				//TypeConverters, where are you ?
				if (propertyInfo.PropertyType.IsEnum && @value is string)
					@value = Enum.Parse (propertyInfo.PropertyType, @value.ToString ());
				setter.Invoke (@object, new [] { @value });
				return;
			}
			throw new Exception (String.Format ("Xaml Parse issue. No Property of name {0} found", propertyName));
		}

		static void SetBinding (this object @object, Type elementType, string propertyName, string bindingString)
		{
			var dotIdx = propertyName.IndexOf ('.');
			if (dotIdx > 0) {
				//Attached DP kind of problem
				var typename = propertyName.Substring (0, dotIdx);
				propertyName = propertyName.Substring (dotIdx + 1);

				elementType = GetElementType ("", typename);
			}
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
			if (@object is BindableObject)
				((BindableObject)@object).SetBinding (property, binding);
			else { //workaround for Templates :(
				var method = @object.GetType ().GetMethod ("SetBinding", new [] {typeof(BindableProperty), typeof(BindingBase)});
				method.Invoke (@object, new object[]{property, binding});
			}

		}

		static Type GetElementType (string namespaceURI, string elementName)
		{
			string ns;
			Assembly asm;

			if (!IsCustom (namespaceURI)) {
				ns = "Xamarin.QuickUI";
				asm = typeof(View).Assembly;
			} else {
				string typename;
				string asmstring;

				ParseXmlns (namespaceURI, out typename, out ns, out asmstring);
				asm = Assembly.Load (asmstring);
			}

			if (elementName.Contains (":"))
				elementName = elementName.Substring (elementName.LastIndexOf (':') + 1);
			return asm.GetType (ns + "." + elementName);
		}

		static object Create (this Type type)
		{
			return type.GetConstructor (new Type[]{ }).Invoke (new object[]{ });
		}

		static object ReadElement (this object @object, Type elementType, XmlReader reader)
		{
 			Debug.Assert (reader.NodeType == XmlNodeType.Element);
			var nodeName = reader.Name;
			object element;
			while (reader.Read ()) {
				switch (reader.NodeType) {
				case XmlNodeType.Element:
					element = GetElementType (reader.NamespaceURI, reader.Name).Create ();
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

		static bool IsCustom (string ns)
		{
			switch (ns) {
			case "http://xamarin.com/quickui":
			case "http://schemas.microsoft.com/winfx/2006/xaml":
			case "":
				return false;
			}
			return true;
		}

		static void ParseXmlns (string xmlns, out string typeName, out string ns, out string asm)
		{
			typeName = ns = asm = null;

			foreach (var decl in xmlns.Split (';')) {
				if (decl.StartsWith ("clr-namespace:", StringComparison.InvariantCulture)) {
					ns = decl.Substring (14, decl.Length - 14);
					continue;
				}
				if (decl.StartsWith ("assembly=", StringComparison.InvariantCulture)) {
					asm = decl.Substring (9, decl.Length - 9);
					continue;
				}
				int nsind = decl.LastIndexOf (".", StringComparison.InvariantCulture);
				if (nsind > 0) {
					ns = decl.Substring (0, nsind);
					typeName = decl.Substring (nsind + 1, decl.Length - nsind - 1);
				} else {
					typeName = decl;
				}
			}
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