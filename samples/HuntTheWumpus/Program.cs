using HyperQ.Learners;
using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HuntTheWumpus
{
    public enum ActionSelectionModel
    {
        PolicyGradient,
        eGreedy,
        GRPO_KL,
        SoftMax,
        SoftMax_KL,
        Hybrid,
        Min,
        Max
    }
    public class ProgramArgs
    {
        public string[] HybridActionModels = null;
        public int epEnd = 0;
        public int epStep = 0;
        public bool qq = false;
        public bool hyper = false;
        public bool layer = false;
        public int warmup_episodes = 0;
        public int memory_size = 500;
        public int dyna_size = 200;
        public int dyna_iters = 500;
        public double dyna_freq = 0.2;
        public bool use_negpos_memory = false;
        public bool onpolicy = true;
        public string save_fname = null;
        public string load_fname = null;
        public bool episodic = false;
        public bool obsession = false;
        public int max_training_steps = 500;
        public QParam g = new QParamExponential(0.997, 1.0, 0.997);
        public QParam e = new QParamExponential(0.5, 0.999991, 0.05);
        public QParam a = new QParamExponential(0.7, 0.999991, 0.1);
        public QParam t = new QParamExponential(0.95, 1, 0.95);
        public QParam h = new QParamExponential(0.0, 1.0, 0.0);
        public List<string> replay = null;
        public RewardModelType model = RewardModelType.Model1;
        public bool randomize = false;
        public Tuple<int, int> Dimensions = new Tuple<int, int>(12, 12);
        public ActionSelectionModel actionModel = ActionSelectionModel.eGreedy;

        public ProgramArgs()
        {
        }
        private QParam ParseParam(string s)
        {
            string[] fields = s.Split(',');
            double v = double.Parse(fields[0]);
            double decay = (fields.Length > 1 ? double.Parse(fields[1]) : 1.0);
            double min = (fields.Length > 2 ? double.Parse(fields[2]) : v);
            return (new QParamExponential(v, decay, min));
        }

        public void Parse(List<string> cmdline)
        {
            for (int i = 0; i < cmdline.Count; i++)
            {
                Console.WriteLine("cmdline: {0}", cmdline[i]);
                int idx = cmdline[i].IndexOf('=');
                if (cmdline[i] == "qq")
                {
                    qq = true;
                }
                else if (cmdline[i] == "hyper")
                {
                    hyper = true;
                }
                else if (cmdline[i] == "layered")
                {
                    layer = true;
                }
                else if (cmdline[i] == "randomize")
                {
                    randomize = true;
                }
                else if (cmdline[i] == "model1")
                {
                    model = RewardModelType.Model1;
                }
                else if (cmdline[i] == "model2")
                {
                    model = RewardModelType.Model2;
                }
                else if (cmdline[i] == "negpos")
                {
                    use_negpos_memory = true;
                }
                else if (cmdline[i] == "offpolicy")
                {
                    onpolicy = false;
                }
                else if (cmdline[i] == "onpolicy")
                {
                    onpolicy = true;
                }
                else if (cmdline[i] == "obsession")
                {
                    obsession = true;
                }
                else if (cmdline[i] == "egreedy")
                {
                    actionModel = ActionSelectionModel.eGreedy;
                }
                else if (cmdline[i] == "grpo")
                {
                    actionModel = ActionSelectionModel.PolicyGradient;
                }
                else if (cmdline[i] == "grpo+kl")
                {
                    actionModel = ActionSelectionModel.GRPO_KL;
                }
                else if (cmdline[i] == "softmax")
                {
                    actionModel = ActionSelectionModel.SoftMax;
                }
                else if (cmdline[i] == "softmax+kl")
                {
                    actionModel = ActionSelectionModel.SoftMax_KL;
                }
                else if (cmdline[i] == "episodic")
                {
                    episodic = true;
                }
                else if (cmdline[i].StartsWith("dims="))
                {
                    string[] sx = cmdline[i].Substring(idx + 1).Split(',', 'x', ' ','+');
                    Dimensions = new Tuple<int,int>(int.Parse(sx[0]), int.Parse(sx[1]));
                }
                else if (cmdline[i].StartsWith("g="))
                {
                    g = ParseParam(cmdline[i].Substring(idx + 1));
                }
                else if (cmdline[i].StartsWith("a="))
                {
                    a = ParseParam(cmdline[i].Substring(idx + 1));
                }
                else if (cmdline[i].StartsWith("e="))
                {
                    e = ParseParam(cmdline[i].Substring(idx + 1));
                }
                else if (cmdline[i].StartsWith("t="))
                {
                    t = ParseParam(cmdline[i].Substring(idx + 1));
                }
                else if (cmdline[i].StartsWith("h="))
                {
                    h = ParseParam(cmdline[i].Substring(idx + 1));
                }
                else if (cmdline[i].StartsWith("dyna="))
                {
                    string[] s = cmdline[i].Substring(idx + 1).Split(',');
                    dyna_size = int.Parse(s[0]);
                    dyna_iters = int.Parse(s[1]);
                }
                else if (cmdline[i].StartsWith("dyna_size="))
                {
                    dyna_size = int.Parse(cmdline[i].Substring(idx + 1));
                }
                else if (cmdline[i].StartsWith("dyna_iters="))
                {
                    dyna_iters = int.Parse(cmdline[i].Substring(idx + 1));
                }
                else if (cmdline[i].StartsWith("dyna_freq="))
                {
                    dyna_freq = double.Parse(cmdline[i].Substring(idx + 1));
                }
                else if (cmdline[i].StartsWith("memory_size=") || cmdline[i].StartsWith("memory="))
                {
                    memory_size = int.Parse(cmdline[i].Substring(idx + 1));
                }
                else if (cmdline[i].StartsWith("warmup="))
                {
                    warmup_episodes = int.Parse(cmdline[i].Substring(idx + 1));
                }
                else if (cmdline[i].StartsWith("save="))
                {
                    save_fname = cmdline[i].Substring(idx + 1);
                }
                else if (cmdline[i].StartsWith("load="))
                {
                    load_fname = cmdline[i].Substring(idx + 1);
                }
                else
                {
                    if (epEnd == 0)
                    {
                        if (!int.TryParse(cmdline[i], out epEnd))
                        {
                            Console.WriteLine("Unrecognized: {0}", cmdline[i]);
                        }
                    }
                    else if (epStep == 0)
                        if (!int.TryParse(cmdline[i], out epStep))
                        {
                            Console.WriteLine("Unrecognized: {0}", cmdline[i]);
                        }
                }
            }
        }

        public void Summarize()
        {
            Console.WriteLine("g={0}, decay={1}, min={2}", g.Value, g.DecayRate, g.MinValue);
            Console.WriteLine("e={0}, decay={1}, min={2}", e.Value, e.DecayRate, e.MinValue);
            Console.WriteLine("a={0}, decay={1}, min={2}", a.Value, a.DecayRate, a.MinValue);
            Console.WriteLine("t={0}, decay={1}, min={2}", t.Value, t.DecayRate, t.MinValue);
            Console.WriteLine("{0} enabled", qq ? "QQ" : "Q");
            if (hyper)
                Console.WriteLine("HYPER enabled");
            if (layer)
                Console.WriteLine("LAYERED HYPER enabled");
            if (use_negpos_memory)
                Console.WriteLine("NEGPOS memory enabled");
            Console.WriteLine("Dyna enabled {0}", dyna_size);
            Console.WriteLine("Dyna freq {0}", dyna_freq);
            Console.WriteLine("Memory enabled {0} {1}", memory_size, episodic ? "EPISODIC" : "");
            Console.WriteLine("warmups enabled {0}", warmup_episodes);
            Console.WriteLine("{0} Policy Evaluation", onpolicy ? "ON" : "OFF");
            if (replay != null)
            {
                Console.WriteLine("Replaying {0} actions", replay.Count);
            }
            if (randomize)
                Console.WriteLine("Randomizing training Worlds");
        }
    }
    internal class Program
    {
        
        // LunarLanderRunner.exe layered qq 200000 100000 memory_size=300 offpolicy negpos dyna_size=0 warmup=46200 save=foo.lqq a=1,.999997,.5 g=0.997,1 e=0.6,0.999991,.1 
        static void Main(string[] args)
        {

            List<string> cmdline = new List<string>();
            List<string> replay = new List<string>();
            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    int idx = args[i].IndexOf('=');
                    if (idx > -1 && args[i].StartsWith("/in="))
                    {
                        Console.WriteLine("Reading input from {0}", args[i].Substring(idx + 1));
                        using (StreamReader sr = new StreamReader(args[i].Substring(idx + 1)))
                        {
                            while (!sr.EndOfStream)
                            {
                                string s = sr.ReadLine();
                                if (s == null)
                                {
                                    break;
                                }
                                s = s.Trim();
                                if (!string.IsNullOrEmpty(s))
                                {
                                    cmdline.Add(s);
                                }
                            }
                        }
                    }
                    else if (idx > -1 && args[i].StartsWith("/replay="))
                    {
                        Console.WriteLine("Reading replay from {0}", args[i].Substring(idx + 1));
                        using (StreamReader sr = new StreamReader(args[i].Substring(idx + 1)))
                        {
                            while (!sr.EndOfStream)
                            {
                                string s = sr.ReadLine();
                                if (s == null)
                                {
                                    break;
                                }
                                s = s.Trim();
                                if (!string.IsNullOrEmpty(s))
                                {
                                    if (s.IndexOf("->") > -1)
                                    {
                                        string[] ee = s.Split('>', '-');
                                        foreach (string sx in ee)
                                        {
                                            Console.WriteLine("REPLAY {0}", sx);
                                            if (string.IsNullOrEmpty(sx))
                                                continue;
                                            replay.Add(sx);
                                        }
                                    }
                                    else
                                        replay.Add(s);
                                }
                            }
                        }
                    }
                    else
                    {
                        cmdline.Add(args[i]);
                    }
                }
            }
            ProgramArgs opts = new ProgramArgs();
            opts.Parse(cmdline);
            if (opts.epEnd == 0)
                opts.epEnd = 5000;
            if (opts.epStep == 0)
                opts.epStep = 500;
            if (replay.Count > 0)
            {
                opts.replay = replay;
            }
            opts.Summarize();
            HyperParams hp = new HyperParams(opts.g, opts.e, opts.a, opts.t, opts.h);
            int numepisodes = opts.epEnd;
            IWumpusRunner runner = null;
            if (opts.qq)
            {
                if (opts.layer)
                {
                    runner = new LayeredHyperQQRunner(numepisodes, hp);
                }
                else if (opts.hyper)
                {
                    runner = new HyperQQRunner(numepisodes, hp);
                }
                else
                    runner = new QQRunner(numepisodes, hp);
            }
            else
            {
                if (opts.layer)
                {
                    runner = new LayeredHyperQRunner(numepisodes, hp);
                }
                else if (opts.hyper)
                {
                    runner = new HyperQRunner(numepisodes, hp);
                }
                else
                    runner = new QRunner(numepisodes, hp);
            }
            QEvalType evalType = opts.onpolicy ? QEvalType.OnPolicy : QEvalType.OffPolicy;
            if (opts.load_fname != null)
            {
                runner.Load(opts.load_fname);
            }
            runner.Run(opts);
            Console.WriteLine("g={0}, decay={1}, min={2}", hp.Gamma.Value, hp.Gamma.DecayRate, hp.Gamma.MinValue);
            Console.WriteLine("e={0}, decay={1}, min={2}", hp.Epsilon.Value, hp.Epsilon.DecayRate, hp.Epsilon.MinValue);
            Console.WriteLine("a={0}, decay={1}, min={2}", hp.Alpha.Value, hp.Alpha.DecayRate, hp.Alpha.MinValue);
            Console.WriteLine("t={0}, decay={1}, min={2}", hp.Tau.Value, hp.Tau.DecayRate, hp.Tau.MinValue);
            if (opts.save_fname != null)
            {
                runner.Save(opts.save_fname);
            }
        }
    }
}
