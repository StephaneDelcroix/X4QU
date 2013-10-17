//
// InternalExtensions.cs
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
using System.Collections;

namespace X4QU
{

	static class InternalExtensions 
	{
		internal static object Create (this Type type)
		{
			return type.GetConstructor (new Type[]{ }).Invoke (new object[]{ });
		}

		internal static object ConvertTo (this object @value, Type toType)
		{
			if (!(value is string))
				return @value;

			//TypeConverters, where are you ?
			if (toType.IsEnum)
				return Enum.Parse (toType, (string)@value);
			if (toType == typeof(Int32))
				return Int32.Parse ((string)@value);
			if (toType == typeof(float))
				return Single.Parse ((string)@value);
			if (toType == typeof(double))
				return Double.Parse ((string)@value);
			return @value;
		}
	}
}