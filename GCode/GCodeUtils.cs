﻿using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using SVG;
using Util;

namespace GCode
{
	/// <summary>
	/// A set of gcode util methods
	/// </summary>
	public static class GCodeUtils
	{
		private readonly static Object writerLock = new Object();
		
		/// <summary>
		/// Split the list of gcode instructions into gcode blocks.
		/// Splits on G0 commands that includes either X and/or Y.
		/// Returns everything before the first G0, all G0 blocks and everything after last block
		/// </summary>
		/// <param name="instructions">list of gcode instructions</param>
		/// <returns>GCode split object</returns>
		public static GCodeGroupObject GroupGCodeInstructions(List<GCodeInstruction> instructions) {

			// list that will be returned
			var allG0 = new List<Point3DBlock>();
			var priorToG0 = new List<GCodeInstruction>();
			var afterLastG0 = new List<GCodeInstruction>();

			// temporary list
			var tempNotG0 = new List<GCodeInstruction>();
			
			float lastX = 0.0f;
			float lastY = 0.0f;
			foreach (var currentInstruction in instructions) {
				
				// Check if this line is a G0 command with an X or Y coordinate
				if (currentInstruction.CommandType == CommandType.RapidMove
				    && (currentInstruction.X.HasValue || currentInstruction.Y.HasValue)) {

					// If x or y here is false we need to use the last coordinate
					// from a previous G0 or G1 as that is where the machine would be
					// or force to 0
					if (!currentInstruction.X.HasValue) {
						currentInstruction.X = lastX;
					}
					if (!currentInstruction.Y.HasValue) {
						currentInstruction.Y = lastY;
					}
					
					// allG0 already has entries,
					// therefore we need to add all the temporary notG0's
					// to the followingLines for the previous entry in allG0
					if (allG0.Count > 0) {
						var lastElement = allG0.Last();
						lastElement.GCodeInstructions.AddRange(tempNotG0);
					}

					// Add this G0 to allG0 with itself (the G0) as the first entry in followingLines
					var point = new Point3DBlock(currentInstruction.X.Value, currentInstruction.Y.Value);
					point.GCodeInstructions.Add(currentInstruction);
					allG0.Add(point);
					
					// Empty notG0 so it's ready for next block
					tempNotG0.Clear();
					
				} else {
					// Add this line to notG0 since it's not a G0 with X or Y coordinates
					tempNotG0.Add(currentInstruction);
				}

				// if allG0 at this point is zero, it means we haven't
				// detected the first G0 group yet.
				// add to the priorToG0 group
				if (allG0.Count == 0) {
					priorToG0.Add(currentInstruction);
				}
				
				// store position state
				if (currentInstruction.X.HasValue) {
					lastX = currentInstruction.X.Value;
				}
				if (currentInstruction.Y.HasValue) {
					lastY = currentInstruction.Y.Value;
				}
			}
			
			// if allG0 at this point is zero, we were unable to parse any
			// movement intstructions, return empty split object
			if (allG0.Count == 0) {
				return null;
			}
			
			// add notG0 to the followingLines for the last entry in allG0
			// this gets the lines after the last G0 in the file
			// we also need to check if the commands here are not G0, G1, G2, G3, or G4
			// because in this case they should be left at the end of the file, not put into the parent G0 block
			foreach (var currentInstruction in tempNotG0) {

				// check if this line is a G0, G1, G2 or G3
				if (currentInstruction.CanRender) {
					// this should be added to the parent G0 block
					allG0.Last().GCodeInstructions.Add(currentInstruction);
				} else {
					// this should be added to the end of the file as it was already there
					afterLastG0.Add(currentInstruction);
				}
			}
			
			// add header and footer as special blocks
			var gcodeBlocks = new GCodeGroupObject();
			gcodeBlocks.G0Sections = allG0;
			gcodeBlocks.PriorToFirstG0Section = priorToG0;
			gcodeBlocks.AfterLastG0Section = afterLastG0;
			
			return gcodeBlocks;
		}
		
		/// <summary>
		/// Sort the list of Point3DBlock by Z-height so that the cutting is incremental
		/// </summary>
		/// <param name="bestPath">Original sequence of the point3d elements</param>
		/// <param name="points">Point elements</param>
		/// <returns>sorted sequence of the point3d elements</returns>
		public static List<int> SortBlocksByZDepth(List<int> bestPath, List<IPoint> points) {
			var sortedBestPath = new List<int>();
			
			Point3DBlock previousBlock = null;
			var blocksWithSameXY = new List<Point3DBlock>();
			
			if (points != null && points.Count > 0 && points[0] is Point3DBlock) {
				
				for (int i = 0; i < bestPath.Count; i++) {
					
					// retrieve correct block using best path index
					var block = points[bestPath[i]] as Point3DBlock;
					
					// if we have blocks available and
					// we have reached a new block (i.e. different X and Y coordinate)
					// store it and clear the blocks
					if (blocksWithSameXY.Count > 0
					    && !block.EqualXYCoordinates(previousBlock)
					   ) {
						// sort the blocks with same xy by z coordinate
						var sortedByZ = blocksWithSameXY.OrderByDescending(s => s.Z);

						// add to return index
						var indexes = sortedByZ.Select(o => o.BestPathIndex);
						sortedBestPath.AddRange(indexes);
						
						// reset block
						blocksWithSameXY.Clear();
					} else {
						// can this happen?
					}

					// store original index
					block.BestPathIndex = bestPath[i];
					
					// retrieve z and store
					float z = RetrieveZ(block.GCodeInstructions);
					if (!float.IsNaN(z)) {
						block.Z = z;

						// add
						blocksWithSameXY.Add(block);
					}
					
					// store previous block
					previousBlock = block;
				}
			}
			
			// also store the last block collection
			// if we have blocks available and
			// we have reached a new block (i.e. different X and Y coordinate)
			// store it and clear the blocks
			if (blocksWithSameXY.Count > 0) {
				// sort the blocks with same xy by z coordinate
				var sortedByZ = blocksWithSameXY.OrderByDescending(s => s.Z);

				// add to return index
				var indexes = sortedByZ.Select(o => o.BestPathIndex);
				sortedBestPath.AddRange(indexes);
				
				// reset block
				blocksWithSameXY.Clear();
			}
			
			return sortedBestPath;
		}
		
		/// <summary>
		/// Search for a normal move with only a z-coordinate
		/// and return the z-coordinate
		/// </summary>
		/// <param name="instructions">list of gcode instructions</param>
		/// <returns>z value or NaN</returns>
		private static float RetrieveZ(List<GCodeInstruction> instructions) {

			// search for a normal move with only a z-coordinate
			float z = 0.0f;
			bool found = false;

			foreach (var instruction in instructions) {
				if ((instruction.CommandType == CommandType.NormalMove
				     || "G38.2".Equals(instruction.Command))
				    && !instruction.X.HasValue
				    && !instruction.Y.HasValue
				    && instruction.Z.HasValue) {
					z = instruction.Z.Value;
					found = true;
					break;
				}
			}
			
			if (!found) {
				// could not found a normal move at all, return what?!
				return float.NaN;
			} else {
				return z;
			}
		}
		
		/// <summary>
		/// Save the gcode instructions assuming the points are Point3DBlock elements
		/// </summary>
		/// <param name="bestPath">best sequence of the point3d elements</param>
		/// <param name="points">Point elements</param>
		/// <param name="filePath">filepath to save</param>
		/// <returns>succesful or not</returns>
		public static bool SaveGCode(List<int> bestPath, List<IPoint> points, string filePath) {
			
			if (points != null && points.Count > 0 && points[0] is Point3DBlock) {
				
				// put all the lines back together in the best order
				var file = new FileInfo(filePath);
				
				if (file.Exists) {
					file.Delete();
				}
				
				using (var tw = new StreamWriter(file.OpenWrite())) {
					
					lock (writerLock) {
						
						tw.WriteLine("(File built with GCodeTools)");
						tw.WriteLine("(Generated on " + DateTime.Now + ")");
						tw.WriteLine();

						for (int i = 0; i < bestPath.Count; i++) {
							
							var block = points[bestPath[i]] as Point3DBlock;
							var instructions = block.GCodeInstructions;

							tw.WriteLine(string.Format("(Start Block_{0})", i));
							foreach (var instruction in instructions) {
								tw.WriteLine(instruction);
							}
							tw.WriteLine(string.Format("(End Block_{0})", i));
							tw.WriteLine();
						}
					}
				}
				
				return true;
				
			} else {
				return false;
			}
		}
		
		/// <summary>
		/// Get the gcode instructions assuming the points are Point3DBlock elements
		/// </summary>
		/// <param name="bestPath">best sequence of the point3d elements</param>
		/// <param name="points">Point elements</param>
		/// <returns>the gcode or null</returns>
		public static String GetGCode(List<int> bestPath, List<IPoint> points) {
			
			if (points != null && points.Count > 0 && points[0] is Point3DBlock) {
				
				using (var tw = new StringWriter()) {
					
					/*
					tw.WriteLine("(File built with GCodeTools)");
					tw.WriteLine("(Generated on " + DateTime.Now + ")");
					tw.WriteLine();
					 */

					for (int i = 0; i < bestPath.Count; i++) {
						
						var block = points[bestPath[i]] as Point3DBlock;
						var instructions = block.GCodeInstructions;

						//tw.WriteLine(string.Format("(Start Block_{0})", i));
						foreach (var instruction in instructions) {
							tw.WriteLine(instruction);
						}
						//tw.WriteLine(string.Format("(End Block_{0})", i));
						tw.WriteLine();
					}
					
					return tw.ToString();
				}
			}
			
			return null;
		}
		
		/// <summary>
		/// Get the gcode instructions using a list of GCodeInstruction elements
		/// </summary>
		/// <param name="instructions">list of instruction elements</param>
		/// <returns>the gcode or null</returns>
		public static String GetGCode(List<GCodeInstruction> instructions) {
			
			using (var tw = new StringWriter()) {
				foreach (var gCodeLine in instructions) {
					tw.WriteLine(gCodeLine);
				}
				return tw.ToString();
			}
		}
		
		/// <summary>
		/// Get the gcode instructions using a list of contour elements
		/// </summary>
		/// <param name="contours">list of contour elements</param>
		/// <param name="z">z height lower / raise</param>
		/// <param name="rapidFeed">feedrate to use for rapid moves</param>
		/// <param name="plungeFeed">feedrate to use for plunge moves (Z)</param>
		/// <param name="safeHeight">z height to raise after each contour action</param>
		/// <returns>the gcode instructions using a list of contour elements</returns>
		public static string GetGCode(IEnumerable<IEnumerable<PointF>>contours, float z, float rapidFeed, float plungeFeed, float safeHeight) {
			
			var sb = new StringBuilder();
			int contourCounter = 0;
			
			// find min and max
			var points = GetPoints(contours);
			
			if (points.Count() == 0) return null;
			
			// Assuming these points come from a SVG
			// we need to shift the Y pos since
			// the SVG origin is upper left, while on
			// this GCode setup it is assumed to be lower left.
			float minX = points.Min(point => point.X);
			//float maxX = points.Max(point => point.X);
			//float minY = points.Min(point => point.Y);
			float maxY = points.Max(point => point.Y);

			// Enumerate each contour in the document
			foreach (var contour in contours)
			{
				contourCounter++;
				bool first = true;
				foreach (var point in contour) {
					if (first) {
						sb.AppendFormat(CultureInfo.InvariantCulture, "G0 X{0:0.##} Y{1:0.##}\n", (point.X - minX), (maxY - point.Y));
						sb.AppendFormat(CultureInfo.InvariantCulture, "G1 Z{0:0.##} F{1:0.##}\n", z, plungeFeed);
						first = false;
					} else {
						sb.AppendFormat(CultureInfo.InvariantCulture, "G1 X{0:0.##} Y{1:0.##} F{2:0.##}\n", (point.X - minX), (maxY - point.Y), rapidFeed);
					}
				}
				sb.AppendFormat(CultureInfo.InvariantCulture, "G0 Z{0:0.##}\n", safeHeight);
			}
			return sb.ToString();
		}

		/// <summary>
		/// Get the gcode instructions for center drilling using a list of contour elements
		/// </summary>
		/// <param name="contours">list of contour elements</param>
		/// <param name="z">z height lower / raise</param>
		/// <param name="rapidFeed">feedrate to use for rapid moves</param>
		/// <param name="plungeFeed">feedrate to use for plunge moves (Z)</param>
		/// <param name="safeHeight">z height to raise after each contour action</param>
		/// <returns>the gcode instructions for center drilling using a list of contour elements</returns>
		public static string GetGCodeCenter(IEnumerable<IEnumerable<PointF>>contours, float z, float rapidFeed, float plungeFeed, float safeHeight) {
			
			var sb = new StringBuilder();
			int contourCounter = 0;

			// find min and max
			var points = GetPoints(contours);
			
			if (points.Count() == 0) return null;
			
			// Assuming these points come from a SVG
			// we need to shift the Y pos since
			// the SVG origin is upper left, while on
			// this GCode setup it is assumed to be lower left.
			float minX = points.Min(point => point.X);
			//float maxX = points.Max(point => point.X);
			//float minY = points.Min(point => point.Y);
			float maxY = points.Max(point => point.Y);
			
			// Enumerate each contour in the document
			foreach (var contour in contours)
			{
				contourCounter++;
				var center = Transformation.Center(contour);
				sb.AppendFormat(CultureInfo.InvariantCulture, "G0 X{0:0.##} Y{1:0.##}\n", (center.X - minX), (maxY - center.Y));
				sb.AppendFormat(CultureInfo.InvariantCulture, "G1 Z{0:0.##} F{1:0.##}\n", z, plungeFeed);
				sb.AppendFormat(CultureInfo.InvariantCulture, "G0 Z{0:0.##}\n", safeHeight);
			}
			return sb.ToString();
		}

		/// <summary>
		/// Dump the raw gcode to file
		/// </summary>
		/// <param name="instructions">list of instruction elements</param>
		/// <param name="filePath">file path</param>
		public static void DumpGCode(List<GCodeInstruction> instructions, string filePath) {
			
			// create or overwrite a file
			using (FileStream f = File.Create(filePath)) {
				using (var s = new StreamWriter(f)) {
					foreach (var gCodeLine in instructions) {
						s.WriteLine(gCodeLine);
					}
				}
			}
		}
		
		/// <summary>
		/// Get the points for all contours in all shapes.
		/// </summary>
		/// <returns>a list of the points</returns>
		public static IEnumerable<PointF> GetPoints(IEnumerable<IEnumerable<PointF>>contours)
		{
			// Enumerate each shape in the document
			foreach (var contour in contours)
			{
				foreach (PointF point in contour)
				{
					yield return point;
				}
			}
		}
		
		/// <summary>
		/// Minimize gcode by removing coordinates that is a repeat of the previous coordinate
		/// </summary>
		/// <param name="instructions">instructions</param>
		/// <returns>minimized gcode</returns>
		public static List<GCodeInstruction> GetMinimizeGCode(List<GCodeInstruction> instructions) {
			
			var cleanedList = new List<GCodeInstruction>();
			var prevInstruction = new GCodeInstruction(CommandType.RapidMove, Point3D.Empty, 0);
			
			CommandType mvtype = CommandType.Other;
			
			float lastX = 0.0f;
			float lastY = 0.0f;
			float lastZ = 0.0f;
			float lastF = 0.0f;
			
			foreach (GCodeInstruction currentInstruction in instructions) {
				
				if (currentInstruction.Equals(prevInstruction)) {
					continue;
				}

				// store move type
				mvtype = currentInstruction.CommandType;
				
				if (mvtype == CommandType.RapidMove
				    || mvtype == CommandType.NormalMove
				    || mvtype == CommandType.CWArc
				    || mvtype == CommandType.CCWArc) {
					
					// merge previous coordinates with newer ones to maintain correct point coordinates
					if ((currentInstruction.X.HasValue || currentInstruction.Y.HasValue || currentInstruction.Z.HasValue
					     || currentInstruction.F.HasValue)) {
						
						if ((currentInstruction.X.HasValue && prevInstruction.X.HasValue
						     && currentInstruction.X.Value == prevInstruction.X.Value)
						    || currentInstruction.X.HasValue && currentInstruction.X.Value == lastX) {
							// X is similar
							currentInstruction.X = (float?) null;
						}
						if ((currentInstruction.Y.HasValue && prevInstruction.Y.HasValue
						     && currentInstruction.Y.Value == prevInstruction.Y.Value)
						    || currentInstruction.Y.HasValue && currentInstruction.Y.Value == lastY) {
							// Y is similar
							currentInstruction.Y = (float?) null;
						}
						if ((currentInstruction.Z.HasValue && prevInstruction.Z.HasValue
						     && currentInstruction.Z.Value == prevInstruction.Z.Value)
						    || currentInstruction.Z.HasValue && currentInstruction.Z.Value == lastZ) {
							// Z is similar
							currentInstruction.Z = (float?) null;
						}
						if ((currentInstruction.F.HasValue && prevInstruction.F.HasValue
						     && currentInstruction.F.Value == prevInstruction.F.Value)
						    || currentInstruction.F.HasValue && currentInstruction.F.Value == lastF) {
							// F is similar
							currentInstruction.F = (float?) null;
						}
					}
					
					// store latest movement instructions as previous instrucion
					prevInstruction = currentInstruction;
					
					// at all times store the latest X, Y, Z and feedrate
					if (currentInstruction.X.HasValue) lastX = currentInstruction.X.Value;
					if (currentInstruction.Y.HasValue) lastY = currentInstruction.Y.Value;
					if (currentInstruction.Z.HasValue) lastZ = currentInstruction.Z.Value;
					if (currentInstruction.F.HasValue) lastF = currentInstruction.F.Value;
				}
				
				cleanedList.Add(currentInstruction);
			}

			return cleanedList;
		}

		/// <summary>
		/// Shift the gcode instructions in x, y or z direction
		/// </summary>
		/// <param name="instructions">list of instruction elements</param>
		/// <param name="deltaX">delta x</param>
		/// <param name="deltaY">delta y</param>
		/// <param name="deltaZ">delta z</param>
		/// <returns>list of shifted gcode</returns>
		public static List<GCodeInstruction> GetShiftedGCode(List<GCodeInstruction> instructions, float deltaX, float deltaY, float deltaZ) {
			
			var shifted = new List<GCodeInstruction>();

			foreach (var gCodeLine in instructions) {
				if (deltaX != 0 || deltaY != 0 || deltaZ != 0) {
					if (deltaX != 0 && gCodeLine.X.HasValue) {
						gCodeLine.X = gCodeLine.X + deltaX;
					}
					if (deltaY != 0 && gCodeLine.Y.HasValue) {
						gCodeLine.Y = gCodeLine.Y + deltaY;
					}
					if (deltaZ != 0 && gCodeLine.Z.HasValue) {
						gCodeLine.Z = gCodeLine.Z + deltaZ;
					}
				}
				shifted.Add(gCodeLine);
			}

			return shifted;
		}
		
		/// <summary>
		/// Shift the gcode instructions in x, y direction
		/// Note! z is not supported
		/// </summary>
		/// <param name="instructions">list of instruction elements</param>
		/// <param name="deltaX">delta x</param>
		/// <param name="deltaY">delta y</param>
		/// <param name="deltaZ">delta z (not used)</param>
		/// <returns>list of shifted gcode</returns>
		public static List<GCodeInstruction> GetShiftedGCodeMatrix(List<GCodeInstruction> instructions, float deltaX, float deltaY, float deltaZ) {
			// Note! the Matrix class doesn't support the z axis
			
			var transformed = new List<GCodeInstruction>();

			// get all points
			var points = instructions.Select(item => item.PointF).ToArray();

			// setup the translation matrix
			using (var matrix = new Matrix()) {
				// setup the matrix to shift with deltax and deltay
				matrix.Translate(deltaX, deltaY);
				
				// transform the points
				matrix.TransformPoints(points);
			}

			// append the transformed points to the list of instructions
			int i = 0;
			foreach (var instruction in instructions) {
				var point = instruction.PointF;
				if (point != PointF.Empty) {
					var transformedPoint = points[i];
					instruction.X = transformedPoint.X;
					instruction.Y = transformedPoint.Y;
					
					transformed.Add(instruction);
				} else {
					transformed.Add(instruction);
				}
				i++;
			}
			
			return transformed;
		}
		
		/// <summary>
		/// Rotate the passed gcode around the center point by the given degees
		/// </summary>
		/// <param name="instructions">list of instruction elements</param>
		/// <param name="center">center point to rotate around</param>
		/// <param name="angle">angle in degrees</param>
		/// <returns>list of rotated gcode</returns>
		public static List<GCodeInstruction> GetRotatedGCodeMatrix(List<GCodeInstruction> instructions, PointF center, float angle) {
			
			// NOTE, THIS DOESN'T SUPPORT ARCS
			
			var transformed = new List<GCodeInstruction>();
			
			// sources
			// https://www.codeproject.com/Articles/8281/Matrix-Transformation-of-Images-using-NET-GDIplus
			// http://csharphelper.com/blog/2015/05/rotate-around-a-point-other-than-the-origin-in-c/
			
			// see setmatrix in
			// https://github.com/bkubicek/grecode/blob/master/main.cpp

			// get all points
			var points = instructions.Select(item => item.PointF).ToArray();

			// setup the rotation matrix
			using (var matrix = new Matrix()) {
				// Translate point to origin
				matrix.Translate(-center.X, -center.Y, MatrixOrder.Append);
				
				// setup the rotation matrix
				matrix.Rotate(angle, MatrixOrder.Append);
				
				// Translate back to original point
				matrix.Translate(center.X, center.Y, MatrixOrder.Append);
				
				// rotate the points
				matrix.TransformPoints(points);
			}
			
			// append the transformed points to the list of instructions
			int i = 0;
			foreach (var instruction in instructions) {
				var point = instruction.PointF;
				if (point != PointF.Empty) {
					var rotatedPoint = points[i];
					instruction.X = rotatedPoint.X;
					instruction.Y = rotatedPoint.Y;
					
					// arc
					//if (instruction.I.HasValue && instruction.J.HasValue) {
					//
					//}
					transformed.Add(instruction);
				} else {
					transformed.Add(instruction);
				}
				i++;
			}
			
			return transformed;
		}
		
		/// <summary>
		/// Rotate the passed gcode around the center point by the given degees
		/// </summary>
		/// <param name="instructions">list of instruction elements</param>
		/// <param name="center">center point to rotate around</param>
		/// <param name="degrees">angle in degrees</param>
		/// <returns>list of rotated gcode</returns>
		public static List<GCodeInstruction> GetRotatedGCode(List<GCodeInstruction> instructions, PointF center, float degrees) {
			
			var transformed = new List<GCodeInstruction>();

			// Sources
			// https://www.codeproject.com/Articles/8281/Matrix-Transformation-of-Images-using-NET-GDIplus
			// http://csharphelper.com/blog/2015/05/rotate-around-a-point-other-than-the-origin-in-c/
			
			// See setmatrix in
			// https://github.com/bkubicek/grecode/blob/master/main.cpp
			
			// Main source
			// https://github.com/thegaragelab/gctools/blob/master/util/filters.py
			
			float i = 0.0f;
			float j = 0.0f;
			float ox = 0.0f;
			float oy = 0.0f;
			float nx = 0.0f;
			float ny = 0.0f;
			
			foreach (var instruction in instructions) {
				if (instruction.HasXY) {
					// I, J are relative to current position so translate before rotating
					if (instruction.I.HasValue && instruction.J.HasValue) {
						i = instruction.I.Value + ox;
						j = instruction.J.Value + oy;
						
						var rotatedIJPoint = Transformation.Rotate(i, j, center.X, center.Y, degrees);
						instruction.I = rotatedIJPoint.X - nx;
						instruction.J = rotatedIJPoint.Y - ny;
					}
					
					// Do the X, Y co-ordinates
					if (instruction.X.HasValue && instruction.Y.HasValue) {
						var rotatedXYPoint = Transformation.Rotate(instruction.X.Value, instruction.Y.Value, center.X, center.Y, degrees);
						instruction.X = rotatedXYPoint.X;
						instruction.Y = rotatedXYPoint.Y;
					}
				}
				transformed.Add(instruction);
				
				// Save position
				ox = instruction.X.HasValue ? instruction.X.Value : ox;
				oy = instruction.Y.HasValue ? instruction.Y.Value : oy;
				
				var rotatedNXYPoint = Transformation.Rotate(ox, oy, center.X, center.Y, degrees);
				nx = rotatedNXYPoint.X;
				ny = rotatedNXYPoint.Y;
			}

			return transformed;
		}
	}
	
	/// <summary>
	/// A class used to organise a gcode file into sections
	/// </summary>
	public class GCodeGroupObject {
		
		public List<Point3DBlock> G0Sections { get; set; }
		public List<GCodeInstruction> PriorToFirstG0Section { get; set; }
		public List<GCodeInstruction> AfterLastG0Section { get; set; }
		
		public override string ToString()
		{
			return string.Format(CultureInfo.CurrentCulture,
			                     "PriorToG0: {0}, AllG0: {1}, Eof: {2}",
			                     this.PriorToFirstG0Section.Count,
			                     this.G0Sections.Count,
			                     this.AfterLastG0Section.Count
			                    );
		}
	}
}
