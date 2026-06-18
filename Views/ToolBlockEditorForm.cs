using System.Drawing;
using System.Windows.Forms;

namespace VisionInspection.Views
{
    /// <summary>
    /// 独立的 WinForms 窗口，托管 CogToolBlockEditV2
    /// 关闭时提示保存 VPP 作业
    /// </summary>
    public class ToolBlockEditorForm : Form
    {
        private Cognex.VisionPro.ToolBlock.CogToolBlockEditV2 _editor;
        private Cognex.VisionPro.ToolBlock.CogToolBlock _toolBlock;
        private System.ComponentModel.IContainer components;

        /// <summary>保存回调：由 MainWindow 注入，返回 true 表示保存成功</summary>
        public System.Func<bool> SaveCallback { get; set; }

        /// <summary>获取保存回调中的当前作业名</summary>
        public System.Func<string> GetJobNameCallback { get; set; }

        public ToolBlockEditorForm(
            Cognex.VisionPro.ToolBlock.CogToolBlock toolBlock,
            Cognex.VisionPro.ToolBlock.CogToolBlockEditV2 existingEditor = null)
        {
            _toolBlock = toolBlock;
            InitializeComponent();

            if (existingEditor != null && !existingEditor.IsDisposed)
            {
                _editor = existingEditor;
            }
            else
            {
                _editor = new Cognex.VisionPro.ToolBlock.CogToolBlockEditV2();
            }

            _editor.Dock = DockStyle.Fill;
            this.Controls.Add(_editor);

            if (toolBlock != null)
            {
                _editor.Subject = toolBlock;
            }

            // 关闭时提示保存
            this.FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    string jobName = GetJobNameCallback?.Invoke() ?? "未命名作业";
                    var result = MessageBox.Show(
                        "是否保存 VPP 作业？\n\n作业: " + jobName +
                        "\n\n[是(Y)] 保存修改到磁盘\n[否(N)] 放弃修改直接关闭\n[取消] 继续编辑",
                        "保存作业",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        bool saved = SaveCallback?.Invoke() ?? false;
                        if (saved)
                        {
                            System.Diagnostics.Debug.WriteLine("VPP 已保存，编辑器关闭");
                            this.Hide();
                            e.Cancel = true;
                        }
                        else
                        {
                            var forceResult = MessageBox.Show(
                                "保存失败！是否仍要关闭（不保存修改）？",
                                "保存失败",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning);
                            if (forceResult == DialogResult.Yes)
                            {
                                System.Diagnostics.Debug.WriteLine("VPP 保存失败，强制关闭（未保存）");
                                this.Hide();
                                e.Cancel = true;
                            }
                        }
                    }
                    else if (result == DialogResult.No)
                    {
                        System.Diagnostics.Debug.WriteLine("VPP 编辑器关闭（用户选择不保存）");
                        this.Hide();
                        e.Cancel = true;
                    }
                    // Cancel → 不做任何操作，继续编辑
                }
            };
        }

        public void UpdateToolBlock(Cognex.VisionPro.ToolBlock.CogToolBlock toolBlock)
        {
            _toolBlock = toolBlock;
            if (_editor != null && !_editor.IsDisposed && toolBlock != null)
            {
                _editor.Subject = toolBlock;
                _editor.PerformLayout();
            }
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.Text = "VisionPro ToolBlock 编辑器";
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;
            this.MinimumSize = new Size(800, 600);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }
    }
}
