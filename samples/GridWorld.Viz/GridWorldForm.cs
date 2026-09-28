using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HyperQ.Samples.GridWorld;
using HyperQ.Learners;
using HyperQ.Util;
using System.Net;

namespace GridWorldViz
{
    public class GridWorldForm : Form
    {
        private PictureBox _gridBox;
        private FlowLayoutPanel _actionHistoryPanel;
        private PictureBox _qBox;
        private PictureBox _qLegendBox;
        private ComboBox _selectorBox;
        private Timer _timer;

        private HyperGridWorld _world;
        private LayeredHyperQ<decimal> _q;
        private IActionSelector<QState<decimal>> _selector;
        private HyperParams _hp;
        private QActionSpace<int> _actionSpace;
        private Queue<Bitmap> _actionHistory = new Queue<Bitmap>();
        private List<PictureBox> _actionPictures = new List<PictureBox>();
        private Bitmap _basic_bmp;
        private GroupBox _rewardGroup;
        private GroupBox _paramGroup;
        private PictureBox _rewardGraphBox;
        private PictureBox _paramGraphBox;
        private RunningAverage _rewardAverage = new RunningAverage();
        private Queue<double> _rewardHistory = new Queue<double>();
        private Queue<double> _epsilonHistory = new Queue<double>();
        private Queue<double> _alphaHistory = new Queue<double>();
        private PictureBox _probBox;
        private GroupBox _probGroup;
        private Dictionary<int, int[]> _actionCounts = new Dictionary<int, int[]>();

        public GridWorldForm()
        {
            Width = 800;
            Height = 600;
            Text = "GridWorld Visualizer Using Layered HyperQ";
            _selectorBox = new ComboBox { Dock = DockStyle.Top };
            _selectorBox.Items.AddRange(new object[]{"eGreedy","PolicyGradient","Max","Min"
                ,"GRPO+KL","SoftMax","SoftMax+KL","PolicyGradient+GRPO","PolicyGradient+GRPO+Max", "GRPO+Max", 
                "eGreedy+GRPO+Max","SoftMax+Max","eGreedy+SoftMax+Max" });
            _selectorBox.SelectedIndex = 0;
            _selectorBox.SelectedIndexChanged += (s,e)=>SetupSelector();

            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill };
            root.RowCount = 2;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 70F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
            Controls.Add(root);
            Controls.Add(_selectorBox);

            Panel top = new Panel { Dock = DockStyle.Fill };
            root.Controls.Add(top, 0, 0);

            _gridBox = new PictureBox{ Width = 300, Height=300, BackColor=Color.Black };
            _probBox = new PictureBox { Width = 300, Height = 300, BackColor = Color.Black };
            _probGroup = new GroupBox { Text = "Action Likelihood", AutoSize = true };
            _probGroup.Controls.Add(_probBox);
            _actionHistoryPanel = new FlowLayoutPanel{ Width = 300, Height=220, FlowDirection = FlowDirection.TopDown, WrapContents=false, AutoScroll = true };

            _qBox = new PictureBox{ Dock = DockStyle.Fill };
            _qLegendBox = new PictureBox{ Width = 50, Dock = DockStyle.Fill};

            FlowLayoutPanel left = new FlowLayoutPanel{ Dock=DockStyle.Left, FlowDirection=FlowDirection.TopDown, AutoSize=true};
            left.Controls.Add(_gridBox);
            left.Controls.Add(_probGroup);
            left.Controls.Add(_actionHistoryPanel);

            TableLayoutPanel qPanel = new TableLayoutPanel{ Dock=DockStyle.Fill };
            qPanel.ColumnCount = 2;
            qPanel.RowCount = 1;
            qPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100F));
            qPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            qPanel.Controls.Add(_qBox,0,0);
            qPanel.Controls.Add(_qLegendBox,1,0);

            top.Controls.Add(qPanel);
            top.Controls.Add(left);

            TableLayoutPanel bottom = new TableLayoutPanel { Dock = DockStyle.Fill };
            bottom.ColumnCount = 2;
            bottom.RowCount = 1;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            root.Controls.Add(bottom, 0, 1);

            _rewardGraphBox = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.White };
            _paramGraphBox = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.White };
            _rewardGroup = new GroupBox { Text = "Running Avg Reward", Dock = DockStyle.Fill };
            _paramGroup = new GroupBox { Text = "Epsilon / Alpha", Dock = DockStyle.Fill };
            _rewardGroup.Controls.Add(_rewardGraphBox);
            _paramGroup.Controls.Add(_paramGraphBox);
            bottom.Controls.Add(_rewardGroup, 0, 0);
            bottom.Controls.Add(_paramGroup, 1, 0);

            _actionHistoryPanel.Height = _qBox.Height;
            _basic_bmp = ActionRowBitmap(new double[] { 0.0, 0.0, 0.0, 0.0 }, -1, _actionHistoryPanel.Width, 20);
            int n_actions = _actionHistoryPanel.Height / 26;
            for (int i = 0; i < n_actions; i++)
            {
                PictureBox pb = new PictureBox { Width = _actionHistoryPanel.Width - 20, Height = 20, Image = _basic_bmp, SizeMode = PictureBoxSizeMode.StretchImage };
                _actionHistoryPanel.Controls.Add(pb);
                _actionPictures.Add(pb);
            }

            InitializeEnv();
            SetupSelector();

            _timer = new Timer();
            _timer.Interval = 200;
            _timer.Tick += (s,e)=>Step();
            _timer.Start();
        }

        private void InitializeEnv()
        {
            QRandom ran = new QRandom(0);
            _world = new HyperGridWorld(4,4,ran);
            _actionSpace = new StaticMappedActionSpace(ran,4);// new QOrdinalActionSpace(ran,4);
            _q = new LayeredHyperQ<decimal>(() => new SingleHyperQ<decimal>(_actionSpace), _actionSpace);
            _world.SpartanMode = true;
            _hp = new HyperParams(0.9,0.4,0.25,0.9999, 1.0, 0.1, 0.05, .75, 0.3, 0.5);
            _world.Reset();
            ResetUI();
        }

        private void SetupSelector()
        {
            string sel = _selectorBox.SelectedItem as string;
            if(sel==null) sel="eGreedy";
            switch(sel)
            {
                case "SoftMax+Max":
                    _selector = new HybridActionSelector<QState<decimal>>(25, new SoftmaxWithKLPenaltyActionSelector<QState<decimal>>(_q, _actionSpace, 0.1), new MaxActionSelector<QState<decimal>>(_actionSpace));
                    break;
                case "eGreedy+SoftMax+Max":
                    _selector = new HybridActionSelector<QState<decimal>>(25, new eGreedyActionSelector<QState<decimal>>(_actionSpace), new SoftmaxWithKLPenaltyActionSelector<QState<decimal>>(_q,_actionSpace, 0.1), new MaxActionSelector<QState<decimal>>(_actionSpace));
                    break;
                case "PolicyGradient+GRPO":
                    _selector = new HybridActionSelector<QState<decimal>>(25, new PolicyGradientActionSelector<QState<decimal>>(_q, _actionSpace), new GRPOWithKLPenaltyActionSelector<QState<decimal>>(_q,_actionSpace, 0.1));
                    break;
                case "PolicyGradient+GRPO+Max":
                    _selector = new HybridActionSelector<QState<decimal>>(25, new PolicyGradientActionSelector<QState<decimal>>(_q, _actionSpace), new GRPOWithKLPenaltyActionSelector<QState<decimal>>(_q, _actionSpace, 0.1), new MaxActionSelector<QState<decimal>>(_actionSpace));
                    break;
                case "GRPO+Max":
                    _selector = new HybridActionSelector<QState<decimal>>(25, new GRPOWithKLPenaltyActionSelector<QState<decimal>>(_q, _actionSpace, 0.1), new MaxActionSelector<QState<decimal>>(_actionSpace));
                    break;
                case "eGreedy+GRPO+Max":
                    _selector = new HybridActionSelector<QState<decimal>>(25, new eGreedyActionSelector<QState<decimal>>(_actionSpace), new GRPOWithKLPenaltyActionSelector<QState<decimal>>(_q, _actionSpace, 0.1), new MaxActionSelector<QState<decimal>>(_actionSpace));
                    break;
                case "PolicyGradient":
                    _selector = new PolicyGradientActionSelector<QState<decimal>>(_q,_actionSpace);
                    break;
                case "GRPO+KL":
                    _selector = new GRPOWithKLPenaltyActionSelector<QState<decimal>>(_q, _actionSpace, 0.1);
                    break;
                case "SoftMax":
                    _selector = new PolicyGradientWithSoftmaxActionSelector<QState<decimal>>(_q, _actionSpace);
                    break;
                case "SoftMax+KL":
                    _selector = new SoftmaxWithKLPenaltyActionSelector<QState<decimal>>(_q, _actionSpace, 0.1);
                    break;
                case "Min":
                    _selector = new MinActionSelector<QState<decimal>>(_actionSpace);
                    break;
                case "Max":
                    _selector = new MaxActionSelector<QState<decimal>>(_actionSpace);
                    break;
                default:
                    _selector = new eGreedyActionSelector<QState<decimal>>(_actionSpace);
                    break;
            }
            _selector.TelemetryCallback = OnTelemetry;
            InitializeEnv();
        }

        private void OnTelemetry(QState<decimal> state, QAction action)
        {
            double[] arr = _q.GetActionArray(state);
            Bitmap bmp = ActionRowBitmap(arr, (int)action.Action, _actionHistoryPanel.Width, 20);
            if(_actionHistory.Count == _actionHistoryPanel.Controls.Count)
            {
                var old = _actionHistory.Dequeue();
                old.Dispose();
            }
            _actionHistory.Enqueue(bmp);
            DrawActionHistory();
            int s = (int)(state[0] * _world.MaxColumns + state[1]);
            if(!_actionCounts.TryGetValue(s, out var cnt))
            {
                cnt = new int[4];
                _actionCounts[s] = cnt;
            }
            if(action.Action >=0 && action.Action < 4)
                cnt[action.Action]++;
        }

        private void Step()
        {
            QState<decimal> state = _world.CurrentQState;
            QAction a = _selector.SelectAction(_q, state, _hp.Epsilon);
            decimal[] up = _world.Update(a);
            _q.OffPolicyUpdate(state, a.Action, _world.CurrentQState, (double)up[1], _hp, EvalMethodType.Max);
            _hp.Decay();
            _rewardAverage.Add((double)up[1]);
            _rewardHistory.Enqueue((double)_rewardAverage.Value);
            if(_rewardHistory.Count>50) _rewardHistory.Dequeue();
            _epsilonHistory.Enqueue(_hp.Epsilon.Value);
            if(_epsilonHistory.Count>50) _epsilonHistory.Dequeue();
            _alphaHistory.Enqueue(_hp.Alpha.Value);
            if(_alphaHistory.Count>50) _alphaHistory.Dequeue();
            DrawGrid();
            DrawActionLikelihoodGrid();
            DrawLayeredQHeatMap();
            DrawRewardGraph();
            DrawParamGraph();
            if(_world.Done)
            {
                _world.Reset();
                ResetUI();
            }
        }

        private void ResetUI()
        {
            foreach (PictureBox pb in _actionPictures)
                pb.Image = _basic_bmp;
            Queue<Bitmap> q = _actionHistory;
            _actionHistory = new Queue<Bitmap>();
            DrawActionHistory();
            foreach (var bmp in q) bmp.Dispose();
            _rewardAverage = new RunningAverage();
            _rewardHistory.Clear();
            _actionCounts.Clear();
            //_epsilonHistory.Clear();
            //_alphaHistory.Clear();
            DrawRewardGraph();
            DrawParamGraph();
            DrawActionLikelihoodGrid();
        }
        private void DrawGrid()
        {
            int rows = _world.MaxRows;
            int cols = _world.MaxColumns;
            Bitmap bmp = new Bitmap(_gridBox.Width, _gridBox.Height);
            using(Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                float cw = (float)_gridBox.Width/cols;
                float ch = (float)_gridBox.Height/rows;
                using(Pen p = new Pen(Color.Gray))
                {
                    for(int i=0;i<=cols;i++)
                        g.DrawLine(p,i*cw,0,i*cw,_gridBox.Height);
                    for(int j=0;j<=rows;j++)
                        g.DrawLine(p,0,j*ch,_gridBox.Width,j*ch);
                }
                using(Brush b = new SolidBrush(Color.Red))
                {
                    g.FillRectangle(b,_world.CurrentColumn*cw,_world.CurrentRow*ch,cw,ch);
                }
                using (Brush b = new SolidBrush(Color.Green))
                {
                    g.FillRectangle(b, _world.GoalColumn * cw, _world.GoalRow * ch, cw, ch);
                }
            }
            _gridBox.Image?.Dispose();
            _gridBox.Image = bmp;
        }

        private void DrawActionHistory()
        {
            if (_actionHistoryPanel.InvokeRequired)
                _actionHistoryPanel.Invoke(new Action(() => { DrawActionHistory(); }));
            else
            {
                _actionHistoryPanel.SuspendLayout();
                int i = 0;
                foreach (var bmp in _actionHistory)
                {
                    PictureBox pictureBox = _actionPictures[i++];
                    pictureBox.Image = bmp;
                    if (i == _actionPictures.Count)
                        break;
                }
                _actionHistoryPanel.ResumeLayout();
            }
        }

        private void DrawLayeredQHeatMap()
        {
            int rows = _world.MaxRows;
            int cols = _world.MaxColumns;
            double min = double.MaxValue;
            double max = double.MinValue;
            for(int r=0;r<rows;r++)
            {
                for(int c=0;c<cols;c++)
                {
                    var layers = _q.GetLayerActionArrays(new QState<decimal>(new decimal[] { r, c }));
                    foreach(var arr in layers)
                    {
                        foreach(var v in arr)
                        {
                            if(v < min) min = v;
                            if(v > max) max = v;
                        }
                    }
                }
            }
            if(Math.Abs(max-min)<1e-9) max = min+1;
            Bitmap bmp = new Bitmap(_qBox.Width, _qBox.Height);
            using(Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                float cw = (float)_qBox.Width/cols;
                float ch = (float)_qBox.Height/rows;
                for(int r=0;r<rows;r++)
                {
                    for(int c=0;c<cols;c++)
                    {
                        float x = c*cw;
                        float y = r*ch;
                        var layers = _q.GetLayerActionArrays(new QState<decimal>(new decimal[]{r,c}));
                        int n = layers.Count;
                        float radius = Math.Min(cw,ch)/2f;
                        for(int i=0;i<n;i++)
                        {
                            double val = layers[i].Max();
                            float t = (float)((val-min)/(max-min));
                            Color color = Blend(Color.Blue,Color.Red,t);
                            float outer = radius*((float)(n-i)/n);
                            float inner = radius*((float)(n-i-1)/n);
                            using(Brush b = new SolidBrush(color))
                                g.FillEllipse(b,x+cw/2-outer,y+ch/2-outer,outer*2,outer*2);
                            if(inner>0)
                                g.FillEllipse(Brushes.Black,x+cw/2-inner,y+ch/2-inner,inner*2,inner*2);
                        }
                    }
                }
                using(Pen p = new Pen(Color.Gray))
                {
                    for(int c=0;c<=cols;c++) g.DrawLine(p,c*cw,0,c*cw,_qBox.Height);
                    for(int r=0;r<=rows;r++) g.DrawLine(p,0,r*ch,_qBox.Width,r*ch);
                }
            }
            _qBox.Image?.Dispose();
            _qBox.Image = bmp;
            _qLegendBox.Image?.Dispose();
            _qLegendBox.Image = LegendBitmap(min,max,_qLegendBox.Width,_qLegendBox.Height,true);
        }
        
        private Bitmap HeatMapBitmap(double[,] data,int w,int h,out double min,out double max,bool grid)
        {
            int rows = data.GetLength(0);
            int cols = data.GetLength(1);
            Bitmap bmp = new Bitmap(w,h);
            min = data.Cast<double>().Min();
            max = data.Cast<double>().Max();
            if(Math.Abs(max-min)<1e-9){max=min+1;}
            using(Graphics g = Graphics.FromImage(bmp))
            {
                float cw = (float)w/cols;
                float ch = (float)h/rows;
                for(int r=0;r<rows;r++)
                {
                    for(int c=0;c<cols;c++)
                    {
                        double val = data[r,c];
                        float t = (float)((val-min)/(max-min));
                        Color color = Blend(Color.Blue,Color.Red,t);
                        using(Brush b = new SolidBrush(color))
                        {
                            g.FillRectangle(b,c*cw,r*ch,cw,ch);
                        }
                    }
                }
                if(grid)
                {
                    using(Pen p = new Pen(Color.Gray))
                    {
                        for(int c=0;c<=cols;c++)
                            g.DrawLine(p,c*cw,0,c*cw,h);
                        for(int r=0;r<=rows;r++)
                            g.DrawLine(p,0,r*ch,w,r*ch);
                    }
                }
            }
            return bmp;
        }

        private Bitmap LegendBitmap(double min,double max,int w,int h,bool vertical)
        {
            Bitmap bmp = new Bitmap(w,h);
            using(Graphics g = Graphics.FromImage(bmp))
            {
                if(vertical)
                {
                    for(int y=0;y<h;y++)
                    {
                        float t = 1f - (float)y/(h-1);
                        using(Pen p = new Pen(Blend(Color.Blue,Color.Red,t)))
                        {
                            g.DrawLine(p,0,y,w,y);
                        }
                    }
                    using(Font f = new Font(FontFamily.GenericSansSerif,8))
                    {
                        g.DrawString(max.ToString("0.00"),f,Brushes.Black,new PointF(0, 0));
                        SizeF sz = g.MeasureString(min.ToString("0.00"),f);
                        g.DrawString(min.ToString("0.00"),f,Brushes.White,new PointF(0,h-sz.Height));
                    }
                }
                else
                {
                    for(int x=0;x<w;x++)
                    {
                        float t = (float)x/(w-1);
                        using(Pen p = new Pen(Blend(Color.Blue,Color.Red,t)))
                        {
                            g.DrawLine(p,x,0,x,h);
                        }
                    }
                    using(Font f = new Font(FontFamily.GenericSansSerif,8))
                    {
                        g.DrawString(min.ToString("0.00"),f,Brushes.White,new PointF(0,0));
                        SizeF sz = g.MeasureString(max.ToString("0.00"),f);
                        g.DrawString(max.ToString("0.00"),f,Brushes.Black,new PointF(w-sz.Width,0));
                    }
                }
            }
            return bmp;
        }

        private Bitmap ActionRowBitmap(double[] arr,int highlight,int w,int h)
        {
            Bitmap bmp = new Bitmap(w,h);
            double min = arr.Min();
            double max = arr.Max();
            if(Math.Abs(max-min)<1e-9) max = min+1;
            using(Graphics g = Graphics.FromImage(bmp))
            {
                float cw = (float)w/arr.Length;
                for(int i=0;i<arr.Length;i++)
                {
                    float t = (float)((arr[i]-min)/(max-min));
                    using (Brush b = new SolidBrush(Blend(Color.Blue,Color.Red,t)))
                    {
                        g.FillRectangle(b,i*cw,0,cw,h);
                    }
                    if(i==highlight)
                    {
                        using(Pen p = new Pen(Color.Green,2))
                        {
                            g.DrawRectangle(p,i*cw,0,cw,h);
                        }
                        if (_selector.LastActionWasRandom)
                        {
                            using (Font f = new Font(FontFamily.GenericSansSerif, 8))
                            {
                                SizeF sz = g.MeasureString("RANDOM", f);
                                g.DrawString("RANDOM", f, Brushes.White, new PointF(i*cw+cw/2 - sz.Width/2, h/2-sz.Height/2));
                            }
                        }
                    }
                }
                using(Pen p = new Pen(Color.Gray))
                {
                    for(int i=0;i<=arr.Length;i++)
                        g.DrawLine(p,i*cw,0,i*cw,h);
                }
            }
            return bmp;
        }

        private void DrawActionLikelihoodGrid()
        {
            int rows = _world.MaxRows;
            int cols = _world.MaxColumns;
            Bitmap bmp = new Bitmap(_probBox.Width, _probBox.Height);
            using(Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                float cw = (float)_probBox.Width/cols;
                float ch = (float)_probBox.Height/rows;
                using(Pen border = new Pen(Color.Gray))
                {
                    for(int r=0;r<rows;r++)
                    {
                        for(int c=0;c<cols;c++)
                        {
                            int state = r*cols + c;
                            double up=0,right=0,down=0,left=0;
                            if(_actionCounts.TryGetValue(state, out var cnts))
                            {
                                double tot = cnts.Sum();
                                if(tot>0)
                                {
                                    up = cnts[0]/tot;
                                    right = cnts[1]/tot;
                                    down = cnts[2]/tot;
                                    left = cnts[3]/tot;
                                }
                            }
                            float x = c*cw;
                            float y = r*ch;
                            using(Brush b = new SolidBrush(Blend(Color.Blue,Color.Red,(float)up)))
                                g.FillRectangle(b,x,y,cw,ch*0.25f);
                            using(Brush b = new SolidBrush(Blend(Color.Blue,Color.Red,(float)right)))
                                g.FillRectangle(b,x+cw*0.75f,y,cw*0.25f,ch);
                            using(Brush b = new SolidBrush(Blend(Color.Blue,Color.Red,(float)left)))
                                g.FillRectangle(b,x,y,cw*0.25f,ch);
                            using(Brush b = new SolidBrush(Blend(Color.Blue,Color.Red,(float)down)))
                                g.FillRectangle(b,x,y+ch*0.75f,cw,ch*0.25f);
                            float dot = Math.Min(cw,ch)*0.1f;
                            g.FillEllipse(Brushes.Black,x+cw/2-dot/2,y+ch/2-dot/2,dot,dot);
                            g.DrawRectangle(border,x,y,cw,ch);
                        }
                    }
                }
            }
            _probBox.Image?.Dispose();
            _probBox.Image = bmp;
        }

        private void DrawRewardGraph()
        {
            Bitmap bmp = new Bitmap(_rewardGraphBox.Width, _rewardGraphBox.Height);
            using(Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                if(_rewardHistory.Count > 1)
                {
                    double min = _rewardHistory.Min();
                    double max = _rewardHistory.Max();
                    if(Math.Abs(max-min)<1e-9) max = min+1;
                    double[] arr = _rewardHistory.ToArray();
                    float stepX = (float)_rewardGraphBox.Width / 49f;
                    for(int i=1;i<arr.Length;i++)
                    {
                        int idx1 = i-1;
                        int idx2 = i;
                        float x1 = _rewardGraphBox.Width - stepX*(_rewardHistory.Count-1-idx1);
                        float x2 = _rewardGraphBox.Width - stepX*(_rewardHistory.Count-1-idx2);
                        float y1 = _rewardGraphBox.Height - (float)((arr[idx1]-min)/(max-min))*_rewardGraphBox.Height;
                        float y2 = _rewardGraphBox.Height - (float)((arr[idx2]-min)/(max-min))*_rewardGraphBox.Height;
                        float t = (float)((arr[idx2]-min)/(max-min));
                        using(Pen p = new Pen(Blend(Color.Blue,Color.Red,t)))
                        {
                            g.DrawLine(p,x1,y1,x2,y2);
                        }
                    }
                }
            }
            _rewardGraphBox.Image?.Dispose();
            _rewardGraphBox.Image = bmp;
        }

        private void DrawParamGraph()
        {
            Bitmap bmp = new Bitmap(_paramGraphBox.Width, _paramGraphBox.Height);
            using(Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                int len = Math.Min(_epsilonHistory.Count, _alphaHistory.Count);
                if(len > 1)
                {
                    double min = 0.0;
                    double max = 100.0;
                    if(Math.Abs(max-min)<1e-9) max = min+1;
                    double[] eps = _epsilonHistory.ToArray();
                    double[] alp = _alphaHistory.ToArray();
                    float stepX = (float)_paramGraphBox.Width / 49f;
                    int HH = _paramGraphBox.Height / 2;
                    for (int i=1;i<len;i++)
                    {
                        int idx1 = i-1;
                        int idx2 = i;
                        float x1 = _paramGraphBox.Width - stepX*(len-1-idx1);
                        float x2 = _paramGraphBox.Width - stepX*(len-1-idx2);
                        float y1e = _paramGraphBox.Height - (float)((eps[idx1] * 100.0-min)/(max-min))*HH;
                        float y2e = _paramGraphBox.Height - (float)((eps[idx2] * 100.0 - min)/(max-min))*HH;
                        float y1a = HH - (float)((alp[idx1] * 100.0 - min)/(max-min))*HH;
                        float y2a = HH - (float)((alp[idx2] * 100.0 - min)/(max-min))*HH;
                        using(Pen p = new Pen(Color.Black))
                        {
                            g.DrawLine(p,x1,y1e,x2,y2e);
                        }
                        using(Pen p = new Pen(Color.Red))
                        {
                            g.DrawLine(p,x1,y1a,x2,y2a);
                        }
                    }
                }
            }
            _paramGraphBox.Image?.Dispose();
            _paramGraphBox.Image = bmp;
        }

        private static Color Blend(Color c1,Color c2,float t)
        {
            t=Math.Max(0,Math.Min(1,t));
            int r=(int)(c1.R+(c2.R-c1.R)*t);
            int g=(int)(c1.G+(c2.G-c1.G)*t);
            int b=(int)(c1.B+(c2.B-c1.B)*t);
            return Color.FromArgb(r,g,b);
        }
    }
}
