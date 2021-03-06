﻿/*
 * Created by SharpDevelop.
 * User: perivar.nerseth
 * Date: 19.06.2017
 * Time: 22.14
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace GCodePlotter
{
	partial class frmOptions
	{
		/// <summary>
		/// Designer variable used to keep track of non-visual components.
		/// </summary>
		private System.ComponentModel.IContainer components = null;
		private System.Windows.Forms.Button btnSave;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox txtLayers;
		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.TextBox txtThickness;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Button btnGeneratePeck;
		
		/// <summary>
		/// Disposes resources used by the form.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing) {
				if (components != null) {
					components.Dispose();
				}
			}
			base.Dispose(disposing);
		}
		
		/// <summary>
		/// This method is required for Windows Forms designer support.
		/// Do not change the method contents inside the source code editor. The Forms designer might
		/// not be able to load this method if it was changed manually.
		/// </summary>
		private void InitializeComponent()
		{
			this.btnSave = new System.Windows.Forms.Button();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.label1 = new System.Windows.Forms.Label();
			this.txtLayers = new System.Windows.Forms.TextBox();
			this.btnCancel = new System.Windows.Forms.Button();
			this.btnGeneratePeck = new System.Windows.Forms.Button();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.txtThickness = new System.Windows.Forms.TextBox();
			this.groupBox1.SuspendLayout();
			this.SuspendLayout();
			// 
			// btnSave
			// 
			this.btnSave.Location = new System.Drawing.Point(129, 194);
			this.btnSave.Name = "btnSave";
			this.btnSave.Size = new System.Drawing.Size(150, 38);
			this.btnSave.TabIndex = 0;
			this.btnSave.Text = "Save";
			this.btnSave.UseVisualStyleBackColor = true;
			this.btnSave.Click += new System.EventHandler(this.BtnSaveClick);
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.txtThickness);
			this.groupBox1.Controls.Add(this.label3);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.btnGeneratePeck);
			this.groupBox1.Controls.Add(this.label1);
			this.groupBox1.Controls.Add(this.txtLayers);
			this.groupBox1.Location = new System.Drawing.Point(12, 12);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(520, 159);
			this.groupBox1.TabIndex = 4;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Drill Peck Values";
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(7, 26);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(193, 48);
			this.label1.TabIndex = 1;
			this.label1.Text = "Enter the Z peck layers (comma separated)";
			// 
			// txtLayers
			// 
			this.txtLayers.Location = new System.Drawing.Point(206, 35);
			this.txtLayers.Name = "txtLayers";
			this.txtLayers.Size = new System.Drawing.Size(285, 26);
			this.txtLayers.TabIndex = 0;
			// 
			// btnCancel
			// 
			this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.btnCancel.Location = new System.Drawing.Point(285, 194);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(138, 38);
			this.btnCancel.TabIndex = 5;
			this.btnCancel.Text = "Cancel";
			this.btnCancel.UseVisualStyleBackColor = true;
			this.btnCancel.Click += new System.EventHandler(this.BtnCancelClick);
			// 
			// btnGeneratePeck
			// 
			this.btnGeneratePeck.Location = new System.Drawing.Point(310, 106);
			this.btnGeneratePeck.Name = "btnGeneratePeck";
			this.btnGeneratePeck.Size = new System.Drawing.Size(101, 36);
			this.btnGeneratePeck.TabIndex = 2;
			this.btnGeneratePeck.Text = "Generate";
			this.btnGeneratePeck.UseVisualStyleBackColor = true;
			this.btnGeneratePeck.Click += new System.EventHandler(this.BtnGeneratePeckClick);
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(6, 84);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(193, 25);
			this.label2.TabIndex = 3;
			this.label2.Text = "Or generate peck values:";
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(7, 109);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(193, 50);
			this.label3.TabIndex = 4;
			this.label3.Text = "Material Thickness \r\n(e.g. MDF 12.7mm)";
			// 
			// txtThickness
			// 
			this.txtThickness.Location = new System.Drawing.Point(206, 111);
			this.txtThickness.Name = "txtThickness";
			this.txtThickness.Size = new System.Drawing.Size(64, 26);
			this.txtThickness.TabIndex = 5;
			this.txtThickness.Text = "12.7";
			// 
			// frmOptions
			// 
			this.AcceptButton = this.btnSave;
			this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this.btnCancel;
			this.ClientSize = new System.Drawing.Size(544, 244);
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.groupBox1);
			this.Controls.Add(this.btnSave);
			this.Name = "frmOptions";
			this.Text = "Please enter default values";
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.ResumeLayout(false);

		}
	}
}
