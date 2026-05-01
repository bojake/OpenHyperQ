using System;
using System.Windows.Forms;
using HuntTheWumpus;
using HyperQ.Util;

namespace WumpusViz
{
    public class OptionsForm : Form
    {
        public ProgramArgs Options { get; private set; }
        TextBox epBox;
        TextBox stepBox;
        TextBox gBox;
        TextBox eBox;
        TextBox aBox;
        TextBox tBox;
        TextBox hBox;
        Button okButton;
        Button cancelButton;

        public OptionsForm(ProgramArgs opts)
        {
            Options = new ProgramArgs();
            Copy(opts, Options);
            Width = 300;
            Height = 250;
            Text = "Program Options";
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8, ColumnCount = 2 };
            Controls.Add(layout);
            layout.Controls.Add(new Label{Text="Episodes"},0,0);
            epBox = new TextBox{Text=Options.epEnd.ToString()};
            layout.Controls.Add(epBox,1,0);
            layout.Controls.Add(new Label{Text="Step"},0,1);
            stepBox = new TextBox{Text=Options.epStep.ToString()};
            layout.Controls.Add(stepBox,1,1);
            layout.Controls.Add(new Label{Text="g"},0,2);
            gBox = new TextBox{Text=ParamString(Options.g)};
            layout.Controls.Add(gBox,1,2);
            layout.Controls.Add(new Label{Text="e"},0,3);
            eBox = new TextBox{Text=ParamString(Options.e)};
            layout.Controls.Add(eBox,1,3);
            layout.Controls.Add(new Label{Text="a"},0,4);
            aBox = new TextBox{Text=ParamString(Options.a)};
            layout.Controls.Add(aBox,1,4);
            layout.Controls.Add(new Label{Text="t"},0,5);
            tBox = new TextBox{Text=ParamString(Options.t)};
            layout.Controls.Add(tBox,1,5);
            layout.Controls.Add(new Label{Text="h"},0,6);
            hBox = new TextBox{Text=ParamString(Options.h)};
            layout.Controls.Add(hBox,1,6);
            FlowLayoutPanel buttons = new FlowLayoutPanel{FlowDirection=FlowDirection.RightToLeft, Dock=DockStyle.Fill};
            okButton = new Button{Text="OK"};
            okButton.Click += (s,e)=>{Apply(); DialogResult=DialogResult.OK; Close();};
            cancelButton = new Button{Text="Cancel"};
            cancelButton.Click += (s,e)=>{DialogResult=DialogResult.Cancel; Close();};
            buttons.Controls.Add(okButton);
            buttons.Controls.Add(cancelButton);
            layout.Controls.Add(buttons,0,7);
            layout.SetColumnSpan(buttons,2);
        }

        private void Copy(ProgramArgs src, ProgramArgs dst)
        {
            dst.epEnd = src.epEnd;
            dst.epStep = src.epStep;
            dst.g = new QParamExponential(src.g.Value, src.g.DecayRate, src.g.MinValue);
            dst.e = new QParamExponential(src.e.Value, src.e.DecayRate, src.e.MinValue);
            dst.a = new QParamExponential(src.a.Value, src.a.DecayRate, src.a.MinValue);
            dst.t = new QParamExponential(src.t.Value, src.t.DecayRate, src.t.MinValue);
            dst.h = new QParamExponential(src.h.Value, src.h.DecayRate, src.h.MinValue);
        }

        private string ParamString(QParam p)
        {
            return string.Format("{0},{1},{2}", p.Value, p.DecayRate, p.MinValue);
        }

        private QParam ParseParam(string s)
        {
            string[] f = s.Split(',');
            double v = double.Parse(f[0]);
            double d = f.Length>1?double.Parse(f[1]):1.0;
            double m = f.Length>2?double.Parse(f[2]):v;
            return new QParamExponential(v,d,m);
        }

        private void Apply()
        {
            int ep; if(int.TryParse(epBox.Text,out ep)) Options.epEnd = ep;
            int st; if(int.TryParse(stepBox.Text,out st)) Options.epStep = st;
            Options.g = ParseParam(gBox.Text);
            Options.e = ParseParam(eBox.Text);
            Options.a = ParseParam(aBox.Text);
            Options.t = ParseParam(tBox.Text);
            Options.h = ParseParam(hBox.Text);
        }
    }
}
