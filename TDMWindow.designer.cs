namespace NATPlugin
{
    partial class TDMWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.LabelTDM = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // LabelTDM
            // 
            this.LabelTDM.AutoSize = true;
            this.LabelTDM.Location = new System.Drawing.Point(12, 71);
            this.LabelTDM.Name = "LabelTDM";
            this.LabelTDM.Size = new System.Drawing.Size(40, 17);
            this.LabelTDM.TabIndex = 1;
            this.LabelTDM.Text = "NATA";
            // 
            // TDMWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.AutoSize = true;
            this.AutoValidate = System.Windows.Forms.AutoValidate.Disable;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(546, 372);
            this.Controls.Add(this.LabelTDM);
            this.ForeColor = System.Drawing.SystemColors.InfoText;
            this.HasMinimizeButton = false;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximumSize = new System.Drawing.Size(550, 400);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(550, 400);
            this.Name = "TDMWindow";
            this.Text = "Expanded Route";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.NATWindow_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private System.Windows.Forms.Label LabelTDM;
    }
}