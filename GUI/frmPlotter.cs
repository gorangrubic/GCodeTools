﻿/**
 * Copyright (c) David-John Miller AKA Anoyomouse 2014
 *
 * See LICENCE in the project directory for licence information
 * Modified by perivar@nerseth.com
 **/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using GCode;

namespace GCodePlotter
{
	public partial class frmPlotter : Form
	{
		private const float DEFAULT_MULTIPLIER = 4.0f;

		private float ZOOMFACTOR = 1.25f;   // = 25% smaller or larger
		private int MINMAX = 8;             // Times bigger or smaller than the ctrl
		
		float scale = 1.0f;
		float multiplier = DEFAULT_MULTIPLIER;

		// calculated total min and max sizes
		float maxX = 0.0f;
		float maxY = 0.0f;
		float maxZ = 0.0f;
		float minX = 0.0f;
		float minY = 0.0f;
		float minZ = 0.0f;
		
		// margins to use within the gcode viewer
		const int LEFT_MARGIN = 20;
		const int BOTTOM_MARGIN = 20;
		
		List<GCodeInstruction> parsedInstructions = null;

		bool bDataLoaded = false;
		
		List<Block> myBlocks;

		Image renderImage = null;
		
		private Point MouseDownLocation;

		public frmPlotter()
		{
			InitializeComponent();
		}
		
		DialogResult AskToLoadData()
		{
			//return MessageBox.Show("Doing this will load/reload data, are you sure you want to load this data deleting your old data?", "Question!", MessageBoxButtons.YesNo);
			return DialogResult.OK;
		}

		#region Events
		void frmPlotterLoad(object sender, EventArgs e)
		{
			bDataLoaded = false;

			var lastFile = QuickSettings.Get["LastOpenedFile"];
			if (!string.IsNullOrWhiteSpace(lastFile))
			{
				// Load data here!
				var fileInfo = new FileInfo(lastFile);
				if (fileInfo.Exists)
				{
					txtFile.Text = fileInfo.Name;
					txtFile.Tag = fileInfo.FullName;
					Application.DoEvents();
					btnParseData.Enabled = true;
					btnParseData.PerformClick();
				}
			}
			
			this.pictureBox1.MouseWheel += OnMouseWheel;
		}

		void frmPlotterResizeEnd(object sender, EventArgs e)
		{
			if (this.WindowState == FormWindowState.Minimized)
			{
				return;
			}
			
			RenderBlocks();
		}

		void TreeViewAfterSelect(object sender, TreeViewEventArgs e)
		{
			RenderBlocks();
			pictureBox1.Refresh();
		}

		void TreeViewMouseDown(object sender, MouseEventArgs e)
		{
			var me = (MouseEventArgs) e;
			if (me.Button == MouseButtons.Right) {
				treeView.SelectedNode = null;
				RenderBlocks();
			}
		}

		void btnLoadClick(object sender, EventArgs e)
		{
			if (AskToLoadData() == DialogResult.No)
			{
				return;
			}

			var result = ofdLoadDialog.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				var file = new FileInfo(ofdLoadDialog.FileName);
				if (!file.Exists)
				{
					MessageBox.Show("Selected file does not exist, please select an existing file!");
					return;
				}

				QuickSettings.Get["LastOpenedFile"] = file.FullName;

				StreamReader tr = file.OpenText();

				txtFile.Text = file.Name;
				txtFile.Tag = file.FullName;

				string data = tr.ReadToEnd();
				tr.Close();
				
				// reset multiplier
				multiplier = DEFAULT_MULTIPLIER;

				ParseText(data);
			}
		}

		void btnParseDataClick(object sender, EventArgs e)
		{
			if (bDataLoaded)
			{
				if (AskToLoadData() == DialogResult.No)
				{
					return;
				}
			}

			if (txtFile.Tag != null) {
				var file = new FileInfo(txtFile.Tag.ToString());
				StreamReader tr = file.OpenText();
				string data = tr.ReadToEnd();
				tr.Close();
				
				ParseText(data);}
		}

		void btnRedrawClick(object sender, EventArgs e)
		{
			// reset multiplier
			multiplier = DEFAULT_MULTIPLIER;
			
			RenderBlocks();
		}

		void btnSplitClick(object sender, EventArgs e)
		{
			if (radLeft.Checked) {
				ResetSplit(0);
			} else {
				ResetSplit(1);
			}
		}
		
		void btnSaveClick(object sender, EventArgs e)
		{
			SaveGCodes(false);
		}

		void btnSaveLayersClick(object sender, EventArgs e)
		{
			SaveGCodes(true);
		}
		
		void btnSaveSplitClick(object sender, EventArgs e)
		{
			if (parsedInstructions == null) {
				MessageBox.Show("No file loaded!");
				return;
			}
			
			if ("".Equals(txtSplit.Text)) {
				MessageBox.Show("No split value entered!");
				return;
			}

			float xSplit = 0.0f;
			if (float.TryParse(txtSplit.Text, out xSplit)) {
				
				btnParseData.Enabled = true;
				btnParseData.PerformClick();
				
				var splitPoint = new Point3D(xSplit, 0, 0);
				
				float zClearance = 2.0f;
				if (!float.TryParse(txtZClearance.Text, out zClearance)) {
					txtZClearance.Text = "2.0";
					zClearance = 2.0f;
				}
				
				var split = GCodeSplitter.Split(parsedInstructions, splitPoint, 0.0f, zClearance);
				
				SaveSplittedGCodes(split, splitPoint, (string)txtFile.Tag);
				
				MessageBox.Show("Saved splitted files to same directory as loaded file.\nExtensions are _first.gcode and _second.gcode!");
			}
		}
		
		void radScaleChange(object sender, EventArgs e)
		{
			RenderBlocks();
		}

		void cbRenderG0CheckedChanged(object sender, EventArgs e)
		{
			if (renderImage == null) {
				return;
			}

			RenderBlocks();
		}
		
		void cbSoloSelectCheckedChanged(object sender, EventArgs e)
		{
			if (renderImage == null) {
				return;
			}

			RenderBlocks();
		}
		
		void PictureBox1MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == System.Windows.Forms.MouseButtons.Right)
			{
				// store mouse down for mouse drag support
				// i.e. change scroll bar position based when dragging
				MouseDownLocation = e.Location;
			}
		}
		
		void PictureBox1MouseMove(object sender, MouseEventArgs e)
		{
			if (e.Button == System.Windows.Forms.MouseButtons.Right)
			{
				// change scroll bar position based when dragging
				var changePoint = new Point(e.Location.X - MouseDownLocation.X,
				                            e.Location.Y - MouseDownLocation.Y);
				
				panelViewer.AutoScrollPosition = new Point(-panelViewer.AutoScrollPosition.X - changePoint.X,
				                                           -panelViewer.AutoScrollPosition.Y - changePoint.Y);
			}
		}

		void OnMouseWheel(object sender, MouseEventArgs mea) {
			
			// http://stackoverflow.com/questions/10694397/how-to-zoom-in-using-mouse-position-on-that-image
			
			if (mea.Delta < 0) {
				ZoomIn(mea.Location);
			} else {
				ZoomOut(mea.Location);
			}
			
			// set handled to true to disable scrolling the scrollbars using the mousewheel
			((HandledMouseEventArgs)mea).Handled = true;
		}
		
		void BtnOptimizeClick(object sender, EventArgs e)
		{
			new GCodeOptimizer.MainForm(null).Show();
		}
		#endregion
		
		#region Private Methods
		void ZoomIn(Point clickPoint) {

			if ((pictureBox1.Width < (MINMAX * panelViewer.Width)) &&
			    (pictureBox1.Height < (MINMAX * panelViewer.Height)))
			{
				// store the multiplier to be used for scrollbar setting later
				float oldMultiplier = multiplier;
				
				// zoom the multiplier
				multiplier *= ZOOMFACTOR;
				
				RenderBlocks();
				UpdateScrollbar(clickPoint, oldMultiplier);
			}
		}

		void ZoomOut(Point clickPoint) {

			if ((pictureBox1.Width > (panelViewer.Width / MINMAX)) &&
			    (pictureBox1.Height > (panelViewer.Height / MINMAX )))
			{
				// store the multiplier to be used for scrollbar setting later
				float oldMultiplier = multiplier;
				
				// zoom the multiplier
				multiplier /= ZOOMFACTOR;

				RenderBlocks();
				UpdateScrollbar(clickPoint, oldMultiplier);
			}

		}
		
		/// <summary>
		/// Update the Scrollbar
		/// </summary>
		/// <param name="clickPoint">position under the cursor which is to be retained</param>
		/// <param name="oldMultiplier">zoom factor between 0.1 and 8.0 before it was updated</param>
		void UpdateScrollbar(Point clickPoint, float oldMultiplier) {
			// http://vilipetek.com/2013/09/07/105/
			
			var scrollPosition = panelViewer.AutoScrollPosition;
			var cursorOffset = new PointF(clickPoint.X + scrollPosition.X,
			                              clickPoint.Y + scrollPosition.Y);
			
			// AutoScrollPosition is quite cumbersome.
			// usually you get negative values when doing this:
			// Point p = this.AutoScrollPosition;
			// but when setting the scroll position you have to use positive values
			// ... so to restore the exact same scroll position you have to invert the negative numbers:
			// this.AutoScrollPosition = new Point(-p.X, -p.Y)
			
			// Calculate the new scroll position
			var newScrollPosition = new Point(
				(int)Math.Round(multiplier * clickPoint.X / oldMultiplier) -
				(int)cursorOffset.X,
				(int)Math.Round(multiplier * clickPoint.Y / oldMultiplier) -
				(int)cursorOffset.Y );
			
			panelViewer.AutoScrollPosition = newScrollPosition;
		}
		
		/// <summary>
		/// Turn the list of instruction into a list of blocks
		/// where the blocks are separated if "cutting path id" is found
		/// and when a rapid move up is found
		/// </summary>
		/// <param name="instructions">list of gcode instructions</param>
		/// <returns>list of blocks</returns>
		static List<Block> GetBlocksOld(List<GCodeInstruction> instructions) {
			
			var currentPoint = Point3D.Empty;
			var blocks = new List<Block>();
			var currentBlock = new Block();
			int blockCounter = 1;
			currentBlock.Name = "Block_" + blockCounter++;
			
			foreach (var currentInstruction in instructions)
			{
				if (currentInstruction.IsOnlyComment) {
					
					if (currentInstruction.Comment.StartsWith("Start cutting path id:")
					    || currentInstruction.Comment == "Footer") {

						if (currentInstruction.Comment == "Footer") {
							currentBlock.Name = currentInstruction.Comment;
						} else {
							if (currentInstruction.Comment.Length > 23) {
								currentBlock.Name = currentInstruction.Comment.Substring(23);
							}
						}
					} else if (currentInstruction.Comment.StartsWith("End cutting path id:")) {
						// disregards blocks if only one entry which is a rapid up move
						if (currentBlock.PlotPoints.Count > 1) {
							blocks.Add(currentBlock);
							
							// Reset block, meaning add new
							currentBlock = new Block();
							currentBlock.Name = "Block_" + blockCounter++;
						}
					} else {
						// ignore all comments up to first "Start Cutting", i.e. header
						// TODO: Handle headers like (Circles) and (Square)
					}
					
				} else if (currentInstruction.CanRender) {
					
					// rapid move up = end of block
					if (currentInstruction.CommandEnum == CommandList.RapidMove
					    && !currentInstruction.X.HasValue
					    && !currentInstruction.Y.HasValue
					    && currentInstruction.Z.HasValue
					    && currentBlock.PlotPoints.Count > 1) {
						
						blocks.Add(currentBlock);
						
						// Reset block, meaning add new
						currentBlock = new Block();
						currentBlock.Name = "Block_" + blockCounter++;
					}
					
					// this is where the block is put together and where the linepoints is added
					var linePointsCollection = currentInstruction.RenderCode(ref currentPoint);
					if (linePointsCollection != null) {
						currentInstruction.CachedLinePoints = linePointsCollection;
						currentBlock.PlotPoints.AddRange(linePointsCollection);
					}

					// make sure to store the actual instruction as well
					currentBlock.GCodeInstructions.Add(currentInstruction);
				} else {
					// ignore everything that isn't a comment and or cannot be rendered
				}
			}

			if (currentBlock.PlotPoints.Count > 0) {
				blocks.Add(currentBlock);
			}

			// remove footer if it exists
			if (blocks.Count > 0) {
				var footer = blocks.Last();
				if (footer.Name == "Footer") {
					blocks.Remove(footer);
				}
			}

			return blocks;
		}

		static List<Block> GetBlocks(List<GCodeInstruction> instructions) {

			var point3DBlocks = GetPoint3DBlocks(instructions);

			var currentPoint = Point3D.Empty;
			var blocks = new List<Block>();
			int blockCounter = 1;
			
			foreach (var currentPoint3D in point3DBlocks) {
				
				var currentBlock = new Block();
				currentBlock.Name = "Block_" + blockCounter++;
				
				foreach (var currentInstruction in currentPoint3D.GCodeInstructions) {
					// this is where the block is put together and where the linepoints is added
					var linePointsCollection = currentInstruction.RenderCode(ref currentPoint);
					if (linePointsCollection != null) {
						currentInstruction.CachedLinePoints = linePointsCollection;
						currentBlock.PlotPoints.AddRange(linePointsCollection);
					}

					// make sure to store the actual instruction as well
					if (currentInstruction.CanRender) {
						currentBlock.GCodeInstructions.Add(currentInstruction);
					}
				}
				
				blocks.Add(currentBlock);
			}
			
			return blocks;
		}
		
		static List<Point3DBlocks> GetPoint3DBlocks(List<GCodeInstruction> instructions) {

			var allG0 = new List<Point3DBlocks>();

			// temporary lists
			var priorToG0 = new List<GCodeInstruction>();
			var notG0 = new List<GCodeInstruction>();
			var eof = new List<GCodeInstruction>();
			
			foreach (var currentInstruction in instructions) {
				
				// check if this line is a G0 command
				if (currentInstruction.CommandEnum == CommandList.RapidMove) {

					// this line is a G0 command, get the X and Y values
					float? x = currentInstruction.X;
					float? y = currentInstruction.Y;

					// check if x or y exist for this line
					if ((x.HasValue || y.HasValue) || (x.HasValue && y.HasValue)) {
						
						// if x or y here is false we need to use the last coordinate from the previous G0 or G1 in followingLines as that is where the machine would be
						if (!y.HasValue && allG0.Count > 0) {
							
							// loop through allG0[-1].followingLines to find the most recent G0 or G1 with a y coordinate

							// We want to use the LINQ to Objects non-invasive
							// Reverse method, not List<T>.Reverse
							foreach (GCodeInstruction item in Enumerable.Reverse(notG0)) {
								if ((item.CanRender)
								    && item.Y.HasValue) {
									// set this y coordinate as y
									y = item.Y.Value;
									break;
								}
							}
						} else if (!x.HasValue && allG0.Count > 0) {
							// loop through allG0[-1].followingLines to find the most recent G0 or G1 with a x coordinate
							
							// We want to use the LINQ to Objects non-invasive
							// Reverse method, not List<T>.Reverse
							foreach (GCodeInstruction item in Enumerable.Reverse(notG0)) {
								if ((item.CanRender)
								    && item.X.HasValue) {
									// set this x coordinate as x
									x = item.X.Value;
									break;
								}
							}
						}

						if (allG0.Count > 0) {
							// allG0 has entries, so we need to add notG0 to the followingLines for the previous entry in allG0
							var lastElement = allG0.Last();
							lastElement.GCodeInstructions.AddRange(notG0);
						}

						// this G0 has a valid X or Y coordinate, add it to allG0 with itself (the G0) as the first entry in followingLines
						var point = new Point3DBlocks(x.Value, y.Value);
						point.GCodeInstructions.Add(currentInstruction);
						allG0.Add(point);
						
						// reset notG0
						notG0.Clear();

					} else {
						// there is no X or Y coordinate for this G0, we can just add it as a normal line
						notG0.Add(currentInstruction);
					}

				} else {
					// add this line to notG0
					notG0.Add(currentInstruction);
				}

				if (allG0.Count == 0) {
					// this holds lines prior to the first G0 for use later
					priorToG0.Add(currentInstruction);
				}
			}
			
			// add notG0 to the followingLines for the last entry in allG0
			// this gets the lines after the last G0 in the file
			// we also need to check if the commands here are not G0, G1, G2, G3, or G4
			// because in this case they should be left at the end of the file, not put into the parent G0 block
			foreach (var currentInstruction in notG0) {

				// check if this line is a G0, G1, G2 or G3
				if (currentInstruction.CanRender) {
					// this should be added to the parent G0 block
					allG0.Last().GCodeInstructions.Add(currentInstruction);
				} else {
					// this should be added to the end of the file as it was already there
					eof.Add(currentInstruction);
				}
			}
			
			return allG0;
		}
		
		void ParseText(string text)
		{
			parsedInstructions = SimpleGCodeParser.ParseText(text);

			treeView.Nodes.Clear();

			// turn the instructions into blocks
			myBlocks = GetBlocks(parsedInstructions);
			
			// calculate max values for X, Y and Z
			// while finalizing the blocks and adding them to the lstPlot
			maxX = 0.0f;
			maxY = 0.0f;
			maxZ = 0.0f;
			minX = 0.0f;
			minY = 0.0f;
			minZ = 0.0f;
			foreach (Block block in myBlocks)
			{
				block.CalculateMinAndMax();
				
				maxX = Math.Max(maxX, block.MaxX);
				maxY = Math.Max(maxY, block.MaxY);
				maxZ = Math.Max(maxZ, block.MaxZ);

				minX = Math.Min(minX, block.MinX);
				minY = Math.Min(minY, block.MinY);
				minZ = Math.Min(minZ, block.MinZ);
				
				// build node tree
				var node = new TreeNode(block.ToString());
				node.Tag = block;
				foreach (var instruction in block.GCodeInstructions) {
					var childNode = new TreeNode();
					childNode.Text = instruction.ToString();
					childNode.Tag = instruction;
					node.Nodes.Add(childNode);
				}
				treeView.Nodes.Add(node);
			}
			
			txtDimension.Text = String.Format("X max: {0:F2} mm \r\nX min: {1:F2} mm\r\nY max: {2:F2} mm \r\nY min: {3:F2} mm \r\nZ max: {4:F2} mm \r\nZ min: {5:F2} mm",
			                                  maxX, minX, maxY, minY, maxZ, minZ);
			
			RenderBlocks();
			bDataLoaded = true;
		}
		
		void ResetSplit(int index) {
			
			if (parsedInstructions == null) {
				MessageBox.Show("No file loaded!");
				return;
			}
			
			if ("".Equals(txtSplit.Text)) {
				MessageBox.Show("No split value entered!");
				return;
			}

			float xSplit = 0.0f;
			if (float.TryParse(txtSplit.Text, out xSplit)) {
				
				btnParseData.Enabled = true;
				btnParseData.PerformClick();
				
				var splitPoint = new Point3D(xSplit, 0, 0);
				
				float zClearance = 2.0f;
				if (!float.TryParse(txtZClearance.Text, out zClearance)) {
					txtZClearance.Text = "2.0";
					zClearance = 2.0f;
				}
				
				var split = GCodeSplitter.Split(parsedInstructions, splitPoint, 0.0f, zClearance);
				
				// clean up the mess with too many G0 commands
				var cleaned = GCodeSplitter.CleanGCode(split[index]);
				
				var gcodeSplitted = Block.BuildGCodeOutput("Block_1", cleaned, false);
				ParseText(gcodeSplitted);
			}
		}
		
		Size GetDimensionsFromZoom() {

			// set scale variable
			scale = (10 * multiplier);

			// 10 mm per grid
			var width = (int)(maxX * scale + 1) / 10 + 2 * LEFT_MARGIN;
			var height = (int)(maxY * scale + 1) / 10 + 2 * BOTTOM_MARGIN;
			
			return new Size(width, height);
		}
		
		void GetEmptyImage() {
			
			var imageDimension = GetDimensionsFromZoom();
			int width = imageDimension.Width;
			int height = imageDimension.Height;
			
			// if anything has changed, reset image
			if (renderImage == null || width != renderImage.Width || height != renderImage.Height)
			{
				if (renderImage != null) {
					renderImage.Dispose();
				}

				renderImage = new Bitmap(width, height);
				pictureBox1.Width = width;
				pictureBox1.Height = height;
				
				try {
					pictureBox1.Image = renderImage;
				} catch (OutOfMemoryException ex) {
					// could draw a red cross like here:
					// http://stackoverflow.com/questions/22163846/zooming-of-an-image-using-mousewheel
				}
			}
		}

		void RenderBlocks()
		{
			GetEmptyImage();
			
			var graphics = Graphics.FromImage(renderImage);
			graphics.Clear(ColorHelper.GetColor(PenColorList.Background));
			
			// draw grid
			Pen gridPen = ColorHelper.GetPen(PenColorList.GridLines);
			for (var x = 0; x < pictureBox1.Width / scale; x++)
			{
				for (var y = 0; y < pictureBox1.Height / scale; y++)
				{
					graphics.DrawLine(gridPen, x * scale + LEFT_MARGIN, 0, x * scale + LEFT_MARGIN, pictureBox1.Height);
					graphics.DrawLine(gridPen, 0, pictureBox1.Height - (y * scale) - BOTTOM_MARGIN, pictureBox1.Width, pictureBox1.Height - (y * scale) - BOTTOM_MARGIN);
				}
			}

			// draw arrow grid
			using (var penZero = new Pen(Color.WhiteSmoke, 1)) {
				graphics.DrawLine(penZero, LEFT_MARGIN, pictureBox1.Height-BOTTOM_MARGIN, pictureBox1.Width, pictureBox1.Height-BOTTOM_MARGIN);
				graphics.DrawLine(penZero, LEFT_MARGIN, 0, LEFT_MARGIN, pictureBox1.Height-BOTTOM_MARGIN);
			}
			using (var penX = new Pen(Color.Red, 3)) {
				penX.StartCap= LineCap.Flat;
				penX.EndCap = LineCap.ArrowAnchor;
				graphics.DrawLine(penX, LEFT_MARGIN, pictureBox1.Height-BOTTOM_MARGIN, 10 * scale + LEFT_MARGIN, pictureBox1.Height-BOTTOM_MARGIN);
			}
			using (var penY = new Pen(Color.Green, 3)) {
				penY.StartCap = LineCap.ArrowAnchor;
				penY.EndCap = LineCap.Flat;
				graphics.DrawLine(penY, LEFT_MARGIN, pictureBox1.Height - (10 * scale) - BOTTOM_MARGIN, LEFT_MARGIN, pictureBox1.Height-BOTTOM_MARGIN);
			}

			// draw gcode
			if (myBlocks != null && myBlocks.Count > 0)
			{
				if (treeView.SelectedNode != null
				    && treeView.SelectedNode.Level == 1) {
					// sub-level, i.e. the instruction level

					var selectedInstruction = (GCodeInstruction) treeView.SelectedNode.Tag;
					
					// find what block this instruction is a part of
					var parentBlock = (Block) treeView.SelectedNode.Parent.Tag;
					
					foreach (var instruction in parentBlock.GCodeInstructions) {
						
						if (instruction == selectedInstruction) {
							foreach (var subLinePlots in instruction.CachedLinePoints) {
								// draw correct instruction as selected
								subLinePlots.DrawSegment(graphics, pictureBox1.Height, true, multiplier, cbRenderG0.Checked, LEFT_MARGIN, BOTTOM_MARGIN);
							}
						} else {
							foreach (var subLinePlots in instruction.CachedLinePoints) {
								subLinePlots.DrawSegment(graphics, pictureBox1.Height, false, multiplier, cbRenderG0.Checked, LEFT_MARGIN, BOTTOM_MARGIN);
							}
						}
					}
				} else {
					// top level, i.e. the block level or if nothing is selected
					foreach (Block blockItem in myBlocks) {
						foreach (var linePlots in blockItem.PlotPoints) {
							
							// check level first
							if (treeView.SelectedNode != null
							    && treeView.SelectedNode.Text.Equals(blockItem.ToString())) {

								// draw correct segment as selected
								linePlots.DrawSegment(graphics, pictureBox1.Height, true, multiplier, cbRenderG0.Checked, LEFT_MARGIN, BOTTOM_MARGIN);
								
							} else {
								// nothing is selected, draw segment as normal
								if (treeView.SelectedNode == null || !cbSoloSelect.Checked) {
									linePlots.DrawSegment(graphics, pictureBox1.Height, false, multiplier, cbRenderG0.Checked, LEFT_MARGIN, BOTTOM_MARGIN);
								}
							}
						}
					}
				}
			}

			pictureBox1.Refresh();
		}
		
		void SaveGCodes(bool doMultiLayer)
		{
			var result = sfdSaveDialog.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				var file = new FileInfo(sfdSaveDialog.FileName);
				if (!doMultiLayer)
				{
					QuickSettings.Get["LastOpenedFile"] = file.FullName;
				}

				if (file.Exists) {
					file.Delete();
				}

				var tw = new StreamWriter(file.OpenWrite());

				tw.WriteLine("(File built with GCodeTools)");
				tw.WriteLine("(Generated on " + DateTime.Now.ToString() + ")");
				tw.WriteLine();
				tw.WriteLine("(Header)");
				tw.WriteLine("G90   (set absolute distance mode)");
				//tw.WriteLine("G90.1 (set absolute distance mode for arc centers)");
				tw.WriteLine("G17   (set active plane to XY)");
				tw.WriteLine("G21   (set units to mm)");
				tw.WriteLine("(Header end.)");
				tw.WriteLine();
				myBlocks.ForEach(x =>
				                 {
				                 	tw.WriteLine();
				                 	tw.Write(x.BuildGCodeOutput(doMultiLayer));
				                 });
				tw.Flush();

				tw.WriteLine();
				tw.WriteLine("(Footer)");
				tw.WriteLine("G00 Z5");
				tw.WriteLine("G00 X0 Y0");
				tw.WriteLine("(Footer end.)");
				tw.WriteLine();

				tw.Flush();
				tw.Close();
			}
		}
		
		void SaveSplittedGCodes(List<List<GCodeInstruction>> split, Point3D splitPoint, string filePath) {
			
			var dirPath = Path.GetDirectoryName(filePath);
			var fileName = Path.GetFileNameWithoutExtension(filePath);
			
			var fileFirst = new FileInfo(dirPath + Path.DirectorySeparatorChar + fileName + "_first.gcode");
			var fileSecond = new FileInfo(dirPath + Path.DirectorySeparatorChar + fileName + "_second.gcode");
			
			// clean them
			var cleanedFirst = GCodeSplitter.CleanGCode(split[0]);
			var cleanedSecond = GCodeSplitter.CleanGCode(split[1]);
			
			SaveGCodes(cleanedFirst, Point3D.Empty, fileFirst);
			SaveGCodes(cleanedSecond, splitPoint, fileSecond);
		}

		void SaveGCodes(List<GCodeInstruction> instructions, Point3D splitPoint, FileInfo file)
		{
			List<Block> blocks = null;
			
			if (splitPoint.IsEmpty) {
				// turn the instructins into blocks
				blocks = GetBlocks(instructions);
			} else {
				// transform instructions
				var transformedInstructions = new List<GCodeInstruction>();
				
				foreach (var instruction in instructions) {
					if (instruction.CanRender) {
						// transform
						if (splitPoint.X > 0 && instruction.X.HasValue) {
							instruction.X = instruction.X - splitPoint.X;
						}
						if (splitPoint.Y > 0 && instruction.Y.HasValue) {
							instruction.Y = instruction.Y - splitPoint.Y;
						}
						if (splitPoint.Z > 0 && instruction.Z.HasValue) {
							instruction.Z = instruction.Z - splitPoint.Z;
						}
					}
					transformedInstructions.Add(instruction);
				}
				
				// turn the instructins into blocks
				blocks =  GetBlocks(transformedInstructions);
			}
			
			if (file.Exists) {
				file.Delete();
			}
			
			var tw = new StreamWriter(file.OpenWrite());

			tw.WriteLine("(File built with GCodeTools)");
			tw.WriteLine("(Generated on " + DateTime.Now.ToString() + ")");
			tw.WriteLine();
			tw.WriteLine("(Header)");
			tw.WriteLine("G90   (set absolute distance mode)");
			//tw.WriteLine("G90.1 (set absolute distance mode for arc centers)");
			tw.WriteLine("G17   (set active plane to XY)");
			tw.WriteLine("G21   (set units to mm)");
			tw.WriteLine("(Header end.)");
			tw.WriteLine();
			
			blocks.ForEach(x =>
			               {
			               	tw.WriteLine();
			               	tw.Write(x.BuildGCodeOutput(false));
			               });
			tw.Flush();

			tw.WriteLine();
			tw.WriteLine("(Footer)");
			tw.WriteLine("G00 Z5");
			tw.WriteLine("G00 X0 Y0");
			tw.WriteLine("(Footer end.)");
			tw.WriteLine();

			tw.Flush();
			tw.Close();
		}
		
		#endregion
	}
}
