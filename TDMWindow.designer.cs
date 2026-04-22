namespace PACOTSPlugin
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
            this.CloseButton = new vatsys.GenericButton();
            this.tdmListView = new System.Windows.Forms.ListView();
            this.SuspendLayout();
            // 
            // CloseButton
            // 
            this.CloseButton.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.CloseButton.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.CloseButton.Font = new System.Drawing.Font("Terminus (TTF)", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            this.CloseButton.Location = new System.Drawing.Point(492, 427);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(79, 38);
            this.CloseButton.SubFont = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.CloseButton.SubText = "";
            this.CloseButton.TabIndex = 17;
            this.CloseButton.Text = "Close";
            this.CloseButton.UseVisualStyleBackColor = true;
            this.CloseButton.Click += new System.EventHandler(this.CloseButton_Click);
            // 
            // tdmListView
            // 
            this.tdmListView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tdmListView.HideSelection = false;
            this.tdmListView.Location = new System.Drawing.Point(12, 29);
            this.tdmListView.Name = "tdmListView";
            this.tdmListView.Scrollable = true;
            this.tdmListView.Size = new System.Drawing.Size(1052, 392);
            this.tdmListView.TabIndex = 19;
            this.tdmListView.UseCompatibleStateImageBehavior = false;
            this.tdmListView.View = System.Windows.Forms.View.Details;
            // 
            // TDMWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.AutoValidate = System.Windows.Forms.AutoValidate.Disable;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(1076, 477);
            this.Controls.Add(this.tdmListView);
            this.Controls.Add(this.CloseButton);
            this.ForeColor = System.Drawing.SystemColors.InfoText;
            this.HasCloseButton = false;
            this.HasMinimizeButton = false;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(400, 200);
            this.Name = "TDMWindow";
            this.Text = "Expanded Route";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.NATWindow_Load);
            this.ResumeLayout(false);

        }

        #endregion
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private vatsys.GenericButton CloseButton;
        private System.Windows.Forms.ListView tdmListView;
    }
}