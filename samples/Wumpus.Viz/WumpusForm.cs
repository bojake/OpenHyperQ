using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using HuntTheWumpus;
using HyperQ.Learners;
using HyperQ.Util;
using HyperQ.Training;
using System.Threading.Tasks;

namespace WumpusViz
{
    public class WumpusForm : Form
    {
        private PictureBox _gridBox;
        private FlowLayoutPanel _actionHistoryPanel;
        private PictureBox _qBox;
        private PictureBox _qLegendBox;
        private ComboBox _selectorBox;
        private ComboBox _runTypeBox;
        private Button _optionsButton;
        private Button _runButton;
        private ProgramArgs _options = new ProgramArgs { epEnd = 100, epStep=50 };
        private IWumpusRunner _runner;
        private double _lastReward = 0.0;
        private uint _totalSteps = 0;

        private Queue<Bitmap> _actionHistory = new Queue<Bitmap>();
        private List<PictureBox> _actionPictures = new List<PictureBox>();
        private Bitmap _basic_bmp;
        private GroupBox _rewardGroup;
        private GroupBox _paramGroup;
        private PictureBox _rewardGraphBox;
        private PictureBox _paramGraphBox;
        private PictureBox _probBox;
        private GroupBox _probGroup;
        private Dictionary<int,int[]> _actionCounts = new Dictionary<int,int[]>();
        private RunningAverage _rewardAverage = new RunningAverage();
        private Queue<decimal> _rewardHistory = new Queue<decimal>();
        private Queue<double> _epsilonHistory = new Queue<double>();
        private Queue<double> _alphaHistory = new Queue<double>();
        private Dictionary<object,double[]> _episodeStates = new Dictionary<object,double[]>(new StateComparer());
        private int _WaitOnStep = 25;
        private volatile bool _isClosing;
        private Task _runTask;
        private bool _running;

        /// <summary>
        /// What the UI needs to draw one training step. Frames are captured on the training thread, which is
        /// the only thread allowed to touch the world and the learner; the UI thread only draws these copies.
        /// </summary>
        private sealed class StepFrame
        {
            public double TotalReward;
            public double Epsilon;
            public double Alpha;
            public CaveItem[,] Map;
            public Tuple<int, int> Player;
            public Tuple<int, int> Dimensions;
        }

        public WumpusForm()
        {
            Width = 800;
            Height = 600;
            Text = "HuntTheWumpus Visualizer";
            FlowLayoutPanel topF = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
            _selectorBox = new ComboBox { Dock = DockStyle.Top };
            _selectorBox.Items.AddRange(new object[]{"eGreedy","PolicyGradient","Max","Min",
                "GRPO+KL","SoftMax","SoftMax+KL","PolicyGradient+GRPO","PolicyGradient+GRPO+Max","GRPO+Max",
                "eGreedy+GRPO+Max","SoftMax+Max","SoftMax_KL+Max","eGreedy+SoftMax+Max","eGreedy+SoftMax_KL+Max"});
            _selectorBox.SelectedIndex = 0;
            // Ensure the selector box is wide enough to display the longest option
            using (Graphics g = _selectorBox.CreateGraphics())
            {
                int width = 0;
                foreach (var item in _selectorBox.Items)
                {
                    int w = (int)g.MeasureString(item.ToString(), _selectorBox.Font).Width;
                    if (w > width) width = w;
                }
                width += SystemInformation.VerticalScrollBarWidth + 20;
                _selectorBox.Width = width;
                _selectorBox.DropDownWidth = width;
            }

            _runTypeBox = new ComboBox { Dock = DockStyle.Top };
            _runTypeBox.Items.AddRange(new object[]{"q","qq","q+hyper","qq+hyper","q+layered","qq+layered"});
            _runTypeBox.SelectedIndex = 0;
            _runTypeBox.SelectedIndexChanged += (s,e)=> { if (!_running) SetupRunner(); };

            _optionsButton = new Button { Text = "Options", Dock = DockStyle.Top };
            _optionsButton.Click += (s,e)=>{
                using(OptionsForm f = new OptionsForm(_options))
                {
                    if(f.ShowDialog()==DialogResult.OK)
                    {
                        _options = f.Options;
                        if (!_running) SetupRunner();
                    }
                }
            };

            _runButton = new Button { Text = "Start", Dock = DockStyle.Top };
            _runButton.Click += (s, e) => {
                if (_running)
                {
                    // The trainer stops at the end of the current episode and then evaluates; the button is
                    // enabled again when the run reports completion.
                    _runner?.Stop();
                    _runButton.Text = "Stopping...";
                    _runButton.Enabled = false;
                }
                else
                {
                    SetupRunner();
                    IWumpusRunner runner = _runner;
                    ProgramArgs options = _options;
                    _running = true;
                    UpdateControls();
                    _runTask = Task.Run(() => RunTraining(runner, options));
                }
            };

            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill };
            root.RowCount = 2;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Percent,70F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,30F));
            Controls.Add(root);
            Controls.Add(topF);
            topF.Controls.Add(_selectorBox);
            topF.Controls.Add(_runTypeBox);
            topF.Controls.Add(_optionsButton);
            topF.Controls.Add(_runButton);

            Panel top = new Panel { Dock = DockStyle.Fill };
            root.Controls.Add(top,0,0);

            _gridBox = new PictureBox{ Width=300, Height=300, BackColor=Color.Black, Dock = DockStyle.Fill, MinimumSize=new Size(300,300) };
            _probBox = new PictureBox { Width = 300, Height = 300, BackColor = Color.Black, Dock = DockStyle.Fill, MinimumSize = new Size(300, 300) };
            _probGroup = new GroupBox { Text = "Action Likelihood", AutoSize = true, Width = 340, Height = 340, MinimumSize = new Size(340, 340) };
            _probGroup.Controls.Add(_probBox);
            _actionHistoryPanel = new FlowLayoutPanel { Width=300, Height=220, FlowDirection=FlowDirection.TopDown, WrapContents=false, AutoScroll=true, Dock = DockStyle.Fill, MinimumSize = new Size(300, 220) };
            _qBox = new PictureBox { Dock = DockStyle.Fill };
            _qLegendBox = new PictureBox{ Width=50, Dock=DockStyle.Fill };

            FlowLayoutPanel left = new FlowLayoutPanel { Dock=DockStyle.Left, FlowDirection=FlowDirection.TopDown, AutoSize=true };
            GroupBox gridGroup = new GroupBox { Text = "Hunt The Wumpus Game", AutoSize = true, MinimumSize = new Size(340, 340) };
            gridGroup.Controls.Add(_gridBox);
            GroupBox actionGroup = new GroupBox { Text = "Action History", AutoSize = true, MinimumSize = new Size(340, 340) };
            actionGroup.Controls.Add(_actionHistoryPanel);
            left.Controls.Add(gridGroup);
            left.Controls.Add(_probGroup);
            left.Controls.Add(actionGroup);

            TableLayoutPanel qPanel = new TableLayoutPanel{ Dock=DockStyle.Fill };
            qPanel.ColumnCount = 2;
            qPanel.RowCount = 1;
            qPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100F));
            qPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            qPanel.Controls.Add(_qBox,0,0);
            qPanel.Controls.Add(_qLegendBox,1,0);

            GroupBox qGroup = new GroupBox { Text = "Q Matrix", Dock = DockStyle.Fill };
            qGroup.Controls.Add(qPanel);
            top.Controls.Add(qGroup);
            top.Controls.Add(left);

            TableLayoutPanel bottom = new TableLayoutPanel { Dock = DockStyle.Fill };
            bottom.ColumnCount = 2;
            bottom.RowCount = 1;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50F));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50F));
            root.Controls.Add(bottom,0,1);

            _rewardGraphBox = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.White };
            _paramGraphBox = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.White };
            _rewardGroup = new GroupBox { Text = "Running Avg Reward", Dock = DockStyle.Fill };
            _paramGroup = new GroupBox { Text = "Epsilon / Alpha", Dock = DockStyle.Fill };
            _rewardGroup.Controls.Add(_rewardGraphBox);
            _paramGroup.Controls.Add(_paramGraphBox);
            bottom.Controls.Add(_rewardGroup,0,0);
            bottom.Controls.Add(_paramGroup,1,0);

            _actionHistoryPanel.Height = _qBox.Height;
            _basic_bmp = ActionRowBitmap(new double[]{0.0,0.0,0.0,0.0,0.0,0.0,0.0,0.0},-1,_actionHistoryPanel.Width,20, false);
            int n_actions = _actionHistoryPanel.Height / 26;
            for(int i=0;i<n_actions;i++)
            {
                PictureBox pb = new PictureBox{ Width=_actionHistoryPanel.Width-20, Height=20, Image=_basic_bmp, SizeMode=PictureBoxSizeMode.StretchImage };
                _actionHistoryPanel.Controls.Add(pb);
                _actionPictures.Add(pb);
            }

            this.FormClosing += WumpusForm_FormClosing;
        }

        private void WumpusForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _isClosing = true;
            _runner?.Stop();
        }

        private void RunOnUiThread(Action action)
        {
            if (action == null || _isClosing || IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (!_isClosing && !IsDisposed && !Disposing)
                        {
                            action();
                        }
                    }));
                    return;
                }

                action();
            }
            catch (ObjectDisposedException)
            {
                // Closing the visualizer intentionally races with the trainer shutdown path.
            }
            catch (InvalidOperationException)
            {
                // The form can be disposed while a background training callback is being marshaled.
            }
        }

        private void InitializeEnv()
        {
            QRandom ran = QRandom.Instance.Seed(0);
            ResetUI();
        }

        private void BuildSelector(string sel)
        {
            switch (sel)
            {
                case "SoftMax+Max":
                case "SoftMax_KL+Max":
                case "eGreedy+SoftMax+Max":
                case "PolicyGradient+GRPO":
                case "PolicyGradient+GRPO+Max":
                case "PolicyGradient+GRPO+Min":
                case "GRPO+Max":
                case "GRPO+Min":
                case "eGreedy+GRPO+Max":
                case "eGreedy+GRPO+Min":
                    _options.actionModel = ActionSelectionModel.Hybrid;
                    _options.HybridActionModels = sel.Split('+');
                    break;
                case "PolicyGradient":
                    _options.actionModel = ActionSelectionModel.PolicyGradient;
                    break;
                case "SoftMax":
                    _options.actionModel = ActionSelectionModel.SoftMax;
                    break;
                case "GRPO+KL":
                    _options.actionModel = ActionSelectionModel.GRPO_KL;
                    break;
                case "SoftMax+KL":
                    _options.actionModel = ActionSelectionModel.SoftMax_KL;
                    break;
                case "Min":
                    _options.actionModel = ActionSelectionModel.Min;
                    break;
                case "Max":
                    _options.actionModel = ActionSelectionModel.Max;
                    break;
                default:
                    _options.actionModel = ActionSelectionModel.eGreedy;
                    break;
            }
        }

        private void SetupRunner()
        {
            string type = _runTypeBox.SelectedItem as string;
            if (type == null) type = "q";
            _options.qq = false;
            _options.hyper = false;
            _options.layer = false;
            BuildSelector(_selectorBox.SelectedItem as string);
            switch(type)
            {
                case "qq+layered":
                    _options.qq = true; _options.layer = true; break;
                case "q+layered":
                    _options.layer = true; break;
                case "q+hyper":
                    _options.hyper = true; break;
                case "qq+hyper":
                    _options.qq = true; _options.hyper = true; break;
                case "qq":
                    _options.qq = true; break;
            }
            HyperParams hp = new HyperParams(_options.g, _options.e, _options.a, _options.t, _options.h);
            int numepisodes = _options.epEnd == 0 ? 100 : _options.epEnd;
            if (_options.qq)
            {
                if (_options.layer)
                    _runner = new LayeredHyperQQRunner(numepisodes, hp);
                else if (_options.hyper)
                    _runner = new HyperQQRunner(numepisodes, hp);
                else
                    _runner = new QQRunner(numepisodes, hp);
            }
            else
            {
                if (_options.layer)
                    _runner = new LayeredHyperQRunner(numepisodes, hp);
                else if (_options.hyper)
                    _runner = new HyperQRunner(numepisodes, hp);
                else
                    _runner = new QRunner(numepisodes, hp);
            }

            InitializeEnv();
            IWumpusRunner runner = _runner;
            // These events fire on the training thread. The world and the learner are read right here, on
            // that thread, and only copies are handed to the UI thread: reading them from the UI thread while
            // training mutates them is a data race (the learner tables are plain dictionaries).
            runner.OnStepEnd += () =>
            {
                System.Threading.Thread.Sleep(_WaitOnStep);
                StepFrame frame = CaptureFrame(runner);
                if (frame != null)
                    RunOnUiThread(() => Step(runner, frame));
            };
            runner.OnEpisodeStart += () => RunOnUiThread(() =>
            {
                if (runner != _runner) return;
                _lastReward = 0;
                ResetUIForEpisode();
            });
            runner.OnTelemetry += (arr, act, rnd) =>
            {
                WumpusBaseGameEnv world = runner.World;
                object state = world != null ? GetCurrentState(world) : null;
                Tuple<int, int> player = world?.PlayerLocation;
                int cols = world != null ? world.Dimensions.Item2 : 0;
                RunOnUiThread(() => OnTelemetry(runner, arr, act, rnd, state, player, cols));
            };
            runner.OnRunComplete += () => RunOnUiThread(() => RunCompleted(runner));
            _totalSteps = 0;
        }

        /// <summary>
        /// Enables the configuration controls only while no run is active: replacing the runner underneath a
        /// running trainer would leave the old run training unobserved.
        /// </summary>
        private void UpdateControls()
        {
            _optionsButton.Enabled = !_running;
            _runTypeBox.Enabled = !_running;
            _selectorBox.Enabled = !_running;
            _runButton.Enabled = true;
            _runButton.Text = _running ? "Stop" : "Start";
        }

        /// <summary>
        /// Runs the training loop on the calling (background) thread and reports a failure on the UI thread
        /// instead of losing it inside an unobserved task.
        /// </summary>
        private void RunTraining(IWumpusRunner runner, ProgramArgs options)
        {
            try
            {
                runner.Run(options);
            }
            catch (Exception ex)
            {
                RunOnUiThread(() =>
                {
                    RunCompleted(runner);
                    MessageBox.Show(this, ex.ToString(), "Training failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
        }

        private void RunCompleted(IWumpusRunner runner)
        {
            if (runner != _runner) return;
            _running = false;
            UpdateControls();
        }

        /// <summary>
        /// Captures the world state for one step. Runs on the training thread.
        /// </summary>
        private StepFrame CaptureFrame(IWumpusRunner runner)
        {
            WumpusBaseGameEnv world = runner.World;
            if (world == null) return null;
            HyperParams hp = runner.Hypers;
            return new StepFrame
            {
                TotalReward = world.Metrics != null ? (double)world.Metrics.TotalReward : 0.0,
                Epsilon = hp != null ? hp.Epsilon.Value : 0.0,
                Alpha = hp != null ? hp.Alpha.Value : 0.0,
                Map = world.GetCaveSnapshot(),
                Player = world.PlayerLocation,
                Dimensions = world.Dimensions
            };
        }

        private object GetCurrentState(WumpusBaseGameEnv world)
        {
            if(world is WumpusHyperGameEnv hw)
                return hw.Discretize();
            if(world is WumpusGameEnv gw)
                return gw.Discretize();
            return null;
        }

        /// <summary>
        /// Records one action selection. Runs on the UI thread with values captured on the training thread:
        /// <paramref name="arr"/> is the learner's action array for <paramref name="state"/>, as computed by the
        /// runner when the action was chosen.
        /// </summary>
        private void OnTelemetry(IWumpusRunner runner, double[] arr, QAction action, bool random, object state, Tuple<int, int> loc, int cols)
        {
            if (runner != _runner || arr == null) return;
            if (state != null)
            {
                _episodeStates[state] = (double[])arr.Clone();
            }
            if (loc != null)
            {
                int s = loc.Item1 * cols + loc.Item2;
                if(!_actionCounts.TryGetValue(s, out var cnt))
                {
                    cnt = new int[4];
                    _actionCounts[s] = cnt;
                }
                if(action.Action >=0 && action.Action < 4) // LURD
                    cnt[action.Action]++;
            }
            Bitmap bmp = ActionRowBitmap(arr,(int)action.Action,_actionHistoryPanel.Width,20, random);
            if(_actionHistory.Count==_actionHistoryPanel.Controls.Count)
            {
                var old=_actionHistory.Dequeue();
                old.Dispose();
            }
            _actionHistory.Enqueue(bmp);
            DrawActionHistory();
        }

        private void Step(IWumpusRunner runner, StepFrame frame)
        {
            if (runner != _runner) return;
            _totalSteps += 1;
            double reward = frame.TotalReward - _lastReward;
            _lastReward = frame.TotalReward;
            _rewardAverage.Add(reward);
            _rewardHistory.Enqueue(_rewardAverage.Value);
            if(_rewardHistory.Count>50) _rewardHistory.Dequeue();
            _epsilonHistory.Enqueue(frame.Epsilon);
            if(_epsilonHistory.Count>50) _epsilonHistory.Dequeue();
            _alphaHistory.Enqueue(frame.Alpha);
            if(_alphaHistory.Count>50) _alphaHistory.Dequeue();
            DrawGrid(frame.Map, frame.Player);
            DrawActionLikelihoodGrid(frame.Dimensions);
            DrawQHeatMap();
            DrawRewardGraph();
            DrawParamGraph();
        }

        private void ResetUIForEpisode()
        {
            _episodeStates.Clear();
            foreach (PictureBox pb in _actionPictures)
                pb.Image = _basic_bmp;
            Queue<Bitmap> q = _actionHistory;
            _actionHistory = new Queue<Bitmap>();
            DrawActionHistory();
            foreach (var bmp in q) bmp.Dispose();
        }
        private void ResetUI()
        {
            ResetUIForEpisode();
            _rewardAverage = new RunningAverage();
            _rewardHistory.Clear();
            _epsilonHistory.Clear();
            _alphaHistory.Clear();
            _actionCounts.Clear();
            _lastReward = 0.0;
            DrawRewardGraph();
            DrawParamGraph();
            // Only called between runs, so the world is not being trained on while it is read here.
            DrawActionLikelihoodGrid(_runner?.World?.Dimensions);
        }

        private void DrawGrid(CaveItem[,] map, Tuple<int, int> loc)
        {
            if (map == null || loc == null) return;
            int rows = map.GetLength(0);
            int cols = map.GetLength(1);
            Bitmap bmp = new Bitmap(_gridBox.Width,_gridBox.Height);
            using(Graphics g = Graphics.FromImage(bmp))
            {
                float cw = (float)_gridBox.Width/cols;
                float ch = (float)_gridBox.Height/rows;
                for(int r=0;r<rows;r++)
                {
                    for(int c=0;c<cols;c++)
                    {
                        Color color = Color.White;
                        string code = null;
                        switch(map[r,c])
                        {
                            case CaveItem.Wall: color = Color.DarkGray; break;
                            case CaveItem.Pit: color = Color.Black; code = "O"; break;
                            case CaveItem.Wumpus: color = Color.Purple; code = "W"; break;
                            case CaveItem.Treasure: color = Color.Gold; code = "$"; break;
                            case CaveItem.Exit: color = Color.Green; code = "S"; break;
                            case CaveItem.Food: color = Color.Orange; code = "F"; break;
                            case CaveItem.Nothing: color = Color.White; break;
                        }
                        using(Brush b = new SolidBrush(color))
                        {
                            g.FillRectangle(b,c*cw,r*ch,cw,ch);
                        }
                        if (code != null)
                        {
                            using (Font f = new Font(FontFamily.GenericSansSerif, 8))
                            {
                                SizeF sz = g.MeasureString(code, f);
                                g.DrawString(code, f, Brushes.White, new PointF(c * cw + cw / 2 - sz.Width / 2,r*ch+ ch / 2 - sz.Height / 2));
                            }
                        }
                    }
                }
                using(Brush b = new SolidBrush(Color.Red))
                {
                    g.FillRectangle(b,loc.Item2*cw,loc.Item1*ch,cw,ch);
                }
                using(Pen p = new Pen(Color.Gray))
                {
                    for(int c=0;c<=cols;c++)
                        g.DrawLine(p,c*cw,0,c*cw,_gridBox.Height);
                    for(int r=0;r<=rows;r++)
                        g.DrawLine(p,0,r*ch,_gridBox.Width,r*ch);
                }
            }
            _gridBox.Image?.Dispose();
            _gridBox.Image = bmp;
        }

        private void DrawActionHistory()
        {
            if (_isClosing || _actionHistoryPanel.IsDisposed || _actionHistoryPanel.Disposing)
            {
                return;
            }

            if(_actionHistoryPanel.InvokeRequired)
            {
                RunOnUiThread(DrawActionHistory);
            }
            else
            {
                _actionHistoryPanel.SuspendLayout();
                int i=0;
                foreach(var bmp in _actionHistory)
                {
                    PictureBox pictureBox = _actionPictures[i++];
                    pictureBox.Image = bmp;
                    if(i==_actionPictures.Count)
                        break;
                }
                _actionHistoryPanel.ResumeLayout();
            }
        }
        private void DrawQHeatMap()
        {
            if(_episodeStates.Count == 0) return;
            int rows = _episodeStates.Count;
            int cols = _episodeStates.Values.First().Length;
            double[,] matrix = new double[rows, cols];
            int r=0;
            foreach(var kv in _episodeStates)
            {
                for(int c=0;c<cols;c++)
                    matrix[r,c] = kv.Value[c];
                r++;
            }
            _qBox.Image?.Dispose();
            double min,max;
            _qBox.Image = HeatMapBitmap(matrix,_qBox.Width,_qBox.Height,out min,out max,true);
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
                        g.DrawString(max.ToString("0.00"),f,Brushes.Black,new PointF(0,0));
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

        private Bitmap ActionRowBitmap(double[] arr,int highlight,int w,int h, bool rnd)
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
                    using(Brush b = new SolidBrush(Blend(Color.Blue,Color.Red,t)))
                    {
                        g.FillRectangle(b,i*cw,0,cw,h);
                    }
                    if(i==highlight)
                    {
                        using(Pen p = new Pen(Color.Green,2))
                        {
                            g.DrawRectangle(p,i*cw,0,cw,h);
                        }
                        if(rnd)
                        {
                            using(Font f = new Font(FontFamily.GenericSansSerif,8))
                            {
                                SizeF sz = g.MeasureString("RND",f);
                                g.DrawString("RND",f,Brushes.White,new PointF(i*cw+cw/2 - sz.Width/2, h/2 - sz.Height/2));
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

        private void DrawActionLikelihoodGrid(Tuple<int, int> dimensions)
        {
            if (dimensions == null) return;
            int rows = dimensions.Item1;
            int cols = dimensions.Item2;
            Bitmap bmp = new Bitmap(_probBox.Width, _probBox.Height);
            using(Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                float cw = (float)_probBox.Width/cols;
                float ch = (float)_probBox.Height/rows;
                float dot = Math.Min(cw, ch) * 0.4f;
                using (Pen border = new Pen(Color.Gray))
                {
                    for(int r=0;r<rows;r++)
                    {
                        for(int c=0;c<cols;c++)
                        {
                            int state = r*cols + c;
                            double up=0,right=0,down=0,left=0,center=0;
                            if(_actionCounts.TryGetValue(state,out var cnt))
                            {
                                // LURD
                                double tot = cnt.Sum();
                                if(tot>0)
                                {
                                    left = cnt[0]/tot;
                                    up = cnt[1]/tot;
                                    right = cnt[2]/tot;
                                    down = cnt[3]/tot;
                                }
                                if (_totalSteps > 0)
                                {
                                    center = tot / _totalSteps;
                                }
                            }
                            float x = c*cw;
                            float y = r*ch;
                            using (Brush b = new SolidBrush(Blend(Color.Blue,Color.Red,(float)up)))
                                g.FillRectangle(b,x,y,cw,ch*0.25f);
                            using(Brush b = new SolidBrush(Blend(Color.Blue,Color.Red,(float)right)))
                                g.FillRectangle(b,x+cw*0.75f,y,cw*0.25f,ch);
                            using(Brush b = new SolidBrush(Blend(Color.Blue,Color.Red,(float)left)))
                                g.FillRectangle(b,x,y,cw*0.25f,ch);
                            using(Brush b = new SolidBrush(Blend(Color.Blue,Color.Red,(float)down)))
                                g.FillRectangle(b,x,y+ch*0.75f,cw,ch*0.25f);
                            using (Brush b = new SolidBrush(Blend(Color.Blue, Color.Red, (float)center)))
                            {
                                g.FillEllipse(b, x + cw / 2 - dot / 2, y + ch / 2 - dot / 2, dot, dot);
                            }
                            g.DrawRectangle(border, x, y, cw, ch);
                        }
                    }
                }
            }
            _probBox.Image?.Dispose();
            _probBox.Image = bmp;
        }

        private void DrawRewardGraph()
        {
            Bitmap bmp = new Bitmap(_rewardGraphBox.Width,_rewardGraphBox.Height);
            using(Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                if(_rewardHistory.Count>1)
                {
                    decimal min = _rewardHistory.Min();
                    decimal max = _rewardHistory.Max();
                    double diff = (double)(max - min);
                    if(Math.Abs(diff)<1e-9) max = min+1;
                    decimal[] arr = _rewardHistory.ToArray();
                    float stepX = (float)_rewardGraphBox.Width/49f;
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
                    // draw vertical axis with min/max labels and tick marks
                    using(Pen p = new Pen(Color.Black))
                    {
                        g.DrawLine(p,0,0,0,_rewardGraphBox.Height);
                        float stepTick = _rewardGraphBox.Height/4f;
                        for(int i=1;i<4;i++)
                        {
                            float y = stepTick*i;
                            g.DrawLine(p,0,y,4,y);
                        }
                    }
                    using(Font f = new Font(FontFamily.GenericSansSerif,8))
                    {
                        g.DrawString(max.ToString("0.##"),f,Brushes.Black,new PointF(2,0));
                        SizeF sz = g.MeasureString(min.ToString("0.##"),f);
                        g.DrawString(min.ToString("0.##"),f,Brushes.Black,new PointF(2,_rewardGraphBox.Height-sz.Height));
                    }
                }
                if(_rewardHistory.Count>0)
                {
                    decimal current = _rewardHistory.Last();
                    using(Font f = new Font(FontFamily.GenericSansSerif,8))
                    {
                        string txt = current.ToString("0.##");
                        SizeF sz = g.MeasureString(txt,f);
                        g.DrawString(txt,f,Brushes.Black,new PointF(_rewardGraphBox.Width-sz.Width-2,_rewardGraphBox.Height-sz.Height-2));
                    }
                }
            }
            _rewardGraphBox.Image?.Dispose();
            _rewardGraphBox.Image = bmp;
        }

        private void DrawParamGraph()
        {
            Bitmap bmp = new Bitmap(_paramGraphBox.Width,_paramGraphBox.Height);
            using(Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                int len = Math.Min(_epsilonHistory.Count,_alphaHistory.Count);
                double min = 0.0;
                double max = 100.0;
                if (len>1)
                {
                    if(Math.Abs(max-min)<1e-9) max = min+1;
                    double[] eps = _epsilonHistory.ToArray();
                    double[] alp = _alphaHistory.ToArray();
                    float stepX = (float)_paramGraphBox.Width/49f;
                    for(int i=1;i<len;i++)
                    {
                        int idx1 = i-1;
                        int idx2 = i;
                        float x1 = _paramGraphBox.Width - stepX*(len-1-idx1);
                        float x2 = _paramGraphBox.Width - stepX*(len-1-idx2);
                        float y1e = _paramGraphBox.Height - (float)((eps[idx1]*100.0-min)/(max-min))*_paramGraphBox.Height;
                        float y2e = _paramGraphBox.Height - (float)((eps[idx2]*100.0-min)/(max-min))*_paramGraphBox.Height;
                        float y1a = _paramGraphBox.Height - (float)((alp[idx1]*100.0-min)/(max-min))*_paramGraphBox.Height;
                        float y2a = _paramGraphBox.Height - (float)((alp[idx2]*100.0-min)/(max-min))*_paramGraphBox.Height;
                        using(Pen p = new Pen(Color.Black))
                        {
                            g.DrawLine(p,x1,y1e,x2,y2e);
                        }
                        using(Pen p = new Pen(Color.Red))
                        {
                            g.DrawLine(p,x1,y1a,x2,y2a);
                        }
                    }
                    // draw vertical axis with min/max labels and tick marks
                    using(Pen p = new Pen(Color.Black))
                    {
                        g.DrawLine(p,0,0,0,_paramGraphBox.Height);
                        float stepTick = _paramGraphBox.Height/4f;
                        for(int i=1;i<4;i++)
                        {
                            float y = stepTick*i;
                            g.DrawLine(p,0,y,4,y);
                        }
                    }
                    using(Font f = new Font(FontFamily.GenericSansSerif,8))
                    {
                        g.DrawString(max.ToString("0"),f,Brushes.Black,new PointF(2,0));
                        SizeF sz = g.MeasureString(min.ToString("0"),f);
                        g.DrawString(min.ToString("0"),f,Brushes.Black,new PointF(2,_paramGraphBox.Height-sz.Height));
                    }
                }
                if(len>0)
                {
                    double epsCur = _epsilonHistory.Last()*100.0;
                    double alpCur = _alphaHistory.Last()*100.0;
                    float x = _paramGraphBox.Width*0.1f;
                    using(Font f = new Font(FontFamily.GenericSansSerif,8))
                    {
                        float yE = _paramGraphBox.Height - (float)((epsCur-min)/(max-min))*_paramGraphBox.Height - 10;
                        float yA = _paramGraphBox.Height - (float)((alpCur-min)/(max-min))*_paramGraphBox.Height + 2;
                        g.DrawString(epsCur.ToString("G4"),f,Brushes.Black,new PointF(x,yE));
                        g.DrawString(alpCur.ToString("G4"),f,Brushes.Red,new PointF(x,yA));
                    }
                }
            }
            _paramGraphBox.Image?.Dispose();
            _paramGraphBox.Image = bmp;
        }

        private static Color Blend(Color c1,Color c2,float t)
        {
            t = Math.Max(0,Math.Min(1,t));
            int r=(int)(c1.R+(c2.R-c1.R)*t);
            int g=(int)(c1.G+(c2.G-c1.G)*t);
            int b=(int)(c1.B+(c2.B-c1.B)*t);
            return Color.FromArgb(r,g,b);
        }

        private class StateComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y) => object.Equals(x, y);
            public int GetHashCode(object obj) => obj?.GetHashCode() ?? 0;
        }
    }
}
