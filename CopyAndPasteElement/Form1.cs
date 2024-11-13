using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CopyAndPasteElement
{
    public partial class Form1 : Form
    {
        int wNumber;//横方向の台数
        int dNumber;//縦方向の台数（間口方向）

        public Form1()
        {
            InitializeComponent();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!Int32.TryParse(textBox1.Text, out wNumber))
            {
                this.DialogResult = DialogResult.Cancel;
            }
            if (!Int32.TryParse(textBox2.Text, out dNumber))
            {
                this.DialogResult = DialogResult.Cancel;
            }
            this.DialogResult = DialogResult.OK;
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            Close();
        }
        public int getWNumber()
        {
            return wNumber;
        }
        public int getDNumber()
        {
            return dNumber;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.textBox2.Focus();
        }
    }
}
