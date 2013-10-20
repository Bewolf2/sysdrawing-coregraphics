//
// System.Drawing.Drawing2D.GraphicsPath-DrawString.cs
//
// Author:
//   Kenneth J. Pouncey (kjpou@pt.lu)
//
// Copyright 2011 Xamarin Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Drawing;

#if MONOMAC
using MonoMac.CoreGraphics;
using MonoMac.AppKit;
using MonoMac.Foundation;
using MonoMac.CoreText;
#else
using MonoTouch.CoreGraphics;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using MonoTouch.CoreText;
#endif

namespace System.Drawing.Drawing2D 
{

	public partial class GraphicsPath
	{

		internal static RectangleF infinite = new RectangleF(-4194304, -4194304, 8388608, 8388608);

		public void AddString(string s,	FontFamily family,	int style, float emSize, Point origin, StringFormat format)
		{
			var font = new Font (family.Name, emSize, (FontStyle)style);
			//var attString = buildAttributedString (s, font, Color.Red);
			var layoutRect = infinite;
			layoutRect.Location = origin;
			NativeDrawString (s, font, Color.Red, layoutRect, format);

		}

		internal void NativeDrawString (string s, Font font, Color brush, RectangleF layoutRectangle, StringFormat stringFormat)
		{
			if (font == null)
				throw new ArgumentNullException ("font");

			if (s == null || s.Length == 0)
				return;

			var attributedString = buildAttributedString(s, font, brush);

			// Work out the geometry
			RectangleF insetBounds = layoutRectangle;

			PointF textPosition = new PointF(insetBounds.X,
			                                 insetBounds.Y);

			float boundsWidth = insetBounds.Width;

			// Calculate the lines
			int start = 0;
			int length = attributedString.Length;

			var typesetter = new CTTypesetter(attributedString);

			float baselineOffset = 0;

			// First we need to calculate the offset for Vertical Alignment if we 
			// are using anything but Top
//			if (stringFormat.LineAlignment != StringAlignment.Near) {
//				while (start < length) {
//					int count = typesetter.SuggestLineBreak (start, boundsWidth);
//					var line = typesetter.GetLine (new NSRange(start, count));
//
//					// Create and initialize some values from the bounds.
//					float ascent;
//					float descent;
//					float leading;
//					line.GetTypographicBounds (out ascent, out descent, out leading);
//					baselineOffset += (float)Math.Ceiling (ascent + descent + leading + 1); // +1 matches best to CTFramesetter's behavior  
//					line.Dispose ();
//					start += count;
//				}
//			}

			start = 0;

			while (start < length && textPosition.Y < insetBounds.Bottom)
			{

				// Now we ask the typesetter to break off a line for us.
				// This also will take into account line feeds embedded in the text.
				//  Example: "This is text \n with a line feed embedded inside it"
				int count = typesetter.SuggestLineBreak(start, boundsWidth);
				var line = typesetter.GetLine(new NSRange(start, count));

				// Create and initialize some values from the bounds.
				float ascent;
				float descent;
				float leading;
				double lineWidth = line.GetTypographicBounds(out ascent, out descent, out leading);
				insetBounds.Width = (float)lineWidth;
				insetBounds.Height = ascent + descent + leading;

				// Calculate the string format if need be
				var penFlushness = 0.0f;

				if (stringFormat.Alignment == StringAlignment.Far)
					penFlushness = (float)line.GetPenOffsetForFlush(1.0f, insetBounds.Width);
				else if (stringFormat.Alignment == StringAlignment.Center)
					penFlushness = (float)line.GetPenOffsetForFlush(0.5f, insetBounds.Width);

				// initialize our Text Matrix or we could get trash in here
				var textMatrix = new CGAffineTransform (
					                 1, 0, 0, -1, 0, ascent);

				if (stringFormat.LineAlignment == StringAlignment.Near)
					textMatrix.Translate (penFlushness + textPosition.X, textPosition.Y); //insetBounds.Height - textPosition.Y -(float)Math.Floor(ascent - 1));
				if (stringFormat.LineAlignment == StringAlignment.Center)
					textMatrix.Translate (penFlushness + textPosition.X, -((insetBounds.Height / 2) + (baselineOffset / 2)) + textPosition.Y);  // -(float)Math.Floor(ascent)
				if (stringFormat.LineAlignment == StringAlignment.Far)
					textMatrix.Translate(penFlushness + textPosition.X, -((insetBounds.Height) + (baselineOffset)) + textPosition.Y);

				var glyphRuns = line.GetGlyphRuns ();

				for (int glyphRunIndex = 0; glyphRunIndex < glyphRuns.Length; glyphRunIndex++)
				{
					
					var glyphRun = glyphRuns [glyphRunIndex];
					var glyphs = glyphRun.GetGlyphs ();
					var glyphPositions = glyphRun.GetPositions ();
					//var textMatrix = glyphRun.TextMatrix;

					// Create and initialize some values from the bounds.
					float glyphAscent;
					float glyphDescent;
					float glyphLeading;

					var elementPoints = new PointF[3];

					for (int glyphIndex = 0; glyphIndex < glyphs.Length; glyphIndex++) 
					{
						if (glyphIndex > 0) 
						{
							textMatrix.x0 += glyphPositions [glyphIndex].X - glyphPositions[glyphIndex - 1].X;
							textMatrix.y0 += glyphPositions [glyphIndex].Y - glyphPositions[glyphIndex - 1].Y;
						}

						var glyphPath = font.nativeFont.GetPathForGlyph (glyphs [glyphIndex]);

						// glyphPath = null if it is a white space character
						if (glyphPath != null) {

							glyphPath.Apply (
								delegate (CGPathElement pathElement) {

										elementPoints[0] = textMatrix.TransformPoint(pathElement.Point1);
										elementPoints[1] = textMatrix.TransformPoint(pathElement.Point2);
								        elementPoints[2] = textMatrix.TransformPoint(pathElement.Point3);
								//Console.WriteLine ("Applying {0} {1} {2} {3}", pathElement.Type, elementPoints[0], elementPoints[1], elementPoints[2]);
										
										
										// now add position offsets

										switch(pathElement.Type)
										{
										case CGPathElementType.MoveToPoint:
											start_new_fig = true;
											Append(elementPoints[0].X, elementPoints[0].Y,PathPointType.Line,true);
											break;
										case CGPathElementType.AddLineToPoint:
											AppendPoint(elementPoints[0], PathPointType.Line, false);
											AppendPoint(elementPoints[0], PathPointType.Line, false);
											break;
										case CGPathElementType.AddCurveToPoint:
										case CGPathElementType.AddQuadCurveToPoint:
											var points = new PointF[] { elementPoints[0], elementPoints[1] };
											var tangents = GeomUtilities.GetCurveTangents (CURVE_MIN_TERMS, points, points.Length, 0.5f, CurveType.Open);
											AppendCurve (points, tangents, 0, points.Length-1, CurveType.Open);
											break;
										case CGPathElementType.CloseSubpath:
											CloseFigure();
											break;
										}
			
								}
		
							);
						}

					}
				}

				// Move the index beyond the line break.
				start += count;
				textPosition.Y += (float)Math.Ceiling(ascent + descent + leading + 1); // +1 matches best to CTFramesetter's behavior  
				line.Dispose();

			}

		}	


		private static NSMutableAttributedString buildAttributedString(string text, Font font, 
		                                                        Color? fontColor=null) 
		{

			// Create a new attributed string definition
			var ctAttributes = new CTStringAttributes ();

			// Font attribute
			ctAttributes.Font = font.nativeFont;
			// -- end font 

			if (fontColor.HasValue) {

				// Font color
				var fc = fontColor.Value;
				var cgColor = new CGColor(fc.R / 255f, 
				                          fc.G / 255f,
				                          fc.B / 255f,
				                          fc.A / 255f);

				ctAttributes.ForegroundColor = cgColor;
				ctAttributes.ForegroundColorFromContext = false;
				// -- end font Color
			}

			if (font.Underline) {
				// Underline
#if MONOMAC
				int single = (int)MonoMac.AppKit.NSUnderlineStyle.Single;
				int solid = (int)MonoMac.AppKit.NSUnderlinePattern.Solid;
				var attss = single | solid;
				ctAttributes.UnderlineStyleValue = attss;
#else
				ctAttributes.UnderlineStyleValue = 1;
#endif
				// --- end underline
			}


			if (font.Strikeout) {
				// StrikeThrough
				//				NSColor bcolor = NSColor.Blue;
				//				NSObject bcolorObject = new NSObject(bcolor.Handle);
				//				attsDic.Add(NSAttributedString.StrikethroughColorAttributeName, bcolorObject);
				//				#if MACOS
				//				int stsingle = (int)MonoMac.AppKit.NSUnderlineStyle.Single;
				//				int stsolid = (int)MonoMac.AppKit.NSUnderlinePattern.Solid;
				//				var stattss = stsingle | stsolid;
				//				var stunderlineObject = NSNumber.FromInt32(stattss);
				//				#else
				//				var stunderlineObject = NSNumber.FromInt32 (1);
				//				#endif
				//
				//				attsDic.Add(StrikethroughStyleAttributeName, stunderlineObject);
				// --- end underline
			}


			// Text alignment
			var alignment = CTTextAlignment.Left;
			var alignmentSettings = new CTParagraphStyleSettings();
			alignmentSettings.Alignment = alignment;
			var paragraphStyle = new CTParagraphStyle(alignmentSettings);

			ctAttributes.ParagraphStyle = paragraphStyle;
			// end text alignment

			NSMutableAttributedString atts = 
				new NSMutableAttributedString(text,ctAttributes.Dictionary);

			return atts;

		}
	}
}

