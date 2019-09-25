using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class TimeGraphSim
    {
        Dictionary<int, string> Nodes;
        Dictionary<string, int> NodesReverse;
        Dictionary<int, HashSet<Tuple<int, string, double>>> Edges;
        Stack<int> currNodeStack;
        DateTime startTime;
        static int dmpCnt = 0;
        public int numPartitions;

        public TimeGraphSim()
        {
            currNodeStack = new Stack<int>();
            Nodes = new Dictionary<int, string>();
            NodesReverse = new Dictionary<string, int>();
            Edges = new Dictionary<int, HashSet<Tuple<int, string, double>>>();
            startTime = DateTime.Now;
            numPartitions = 0;
            //Nodes.Add(0, "main");
            //Push(0);
        }

        public int Count()
        {
            return Nodes.Count;
        }


        public void AddEdge(int n1, int n2, string label1, double label2)
        {
            if (!Edges.ContainsKey(n1))
                Edges.Add(n1, new HashSet<Tuple<int, string, double>>());
            Edges[n1].Add(Tuple.Create(n2, label1, label2));
        }

        public string ToDot()
        {
            string str;
            str = "digraph TG {" + "\n";
            foreach (var tup in Nodes)
            {
                //str = str + ("{0} [label=\"{1}\"]\n", tup.Key, tup.Value);
                str = str + tup.Key.ToString() + " [label=\"" + tup.Value.ToString() + "\"]\n";
            }
            foreach (var tup in Edges)
            {
                foreach (var tgt in tup.Value)
                    //str = str + ("{0} -> {1} [label=\"{2} {3}\"]\n", tup.Key, tgt.Item1, tgt.Item2, tgt.Item3.ToString("F2"));
                    str = str + tup.Key.ToString() + " -> " + tgt.Item1.ToString() + " [label=\"" + tgt.Item2.ToString() + " " + tgt.Item3.ToString("F2") + "\"]\n";
            }
            str = str + "}";
            //using (var fs = new System.IO.StreamWriter("tg" + (dmpCnt++) + ".dot"))
            //{
            //    fs.WriteLine("digraph TG {");
            //
            //    foreach (var tup in Nodes)
            //    {
            //        fs.WriteLine("{0} [label=\"{1}\"]", tup.Key, tup.Value);
            //    }
            //
            //    foreach (var tup in Edges)
            //    {
            //        foreach (var tgt in tup.Value)
            //            fs.WriteLine("{0} -> {1} [label=\"{2} {3}\"]", tup.Key, tgt.Item1, tgt.Item2, tgt.Item3.ToString("F2"));
            //    }
            //
            //    fs.WriteLine("}");
            //
            //}
            return str;
        }

        void Push(int node)
        {
            currNodeStack.Push(node);
        }

        public void Pop(int n)
        {
            while (n > 0) { n--; currNodeStack.Pop(); }
            startTime = DateTime.Now;
        }

        //public void AddEdge(string tgt, string label)
        //{
        //   numPartitions++;
        //    var tgtnode = Nodes.Count;
        //    Nodes.Add(tgtnode, tgt);

        //    AddEdge(currNodeStack.Peek(), tgtnode, label, (DateTime.Now - startTime).TotalSeconds);
        //    Push(tgtnode);
        //    startTime = DateTime.Now;
        //}

        public void AddEdge(string parent, string child, double timeTaken)
        {
            //Log.WriteLine(Log.Info, string.Format(parent + " " + child + " " + timeTaken.ToString()));
            Debug.Assert(Nodes.Values.Contains(parent));
            numPartitions++;
            if (Nodes.Values.Contains(parent))
            {
                //Log.WriteLine(Log.Info, string.Format("inside if"));
                var tgtnode = Nodes.Count;
                Nodes.Add(tgtnode, child);
                NodesReverse.Add(child, tgtnode);
                AddEdge(NodesReverse[parent], tgtnode, "split", timeTaken);
            }
            else
            {
                //Log.WriteLine(Log.Info, string.Format("inside else"));
                var srcnode = Nodes.Count;
                Nodes.Add(srcnode, parent);
                NodesReverse.Add(parent, srcnode);
                var tgtnode = Nodes.Count;
                Nodes.Add(tgtnode, child);
                NodesReverse.Add(child, tgtnode);
                AddEdge(srcnode, tgtnode, "split", timeTaken);
            }
        }

        //public void AddEdgeDone(string label)
        //{
        //    numPartitions++;
        //    var tgtnode = Nodes.Count;
        //    Nodes.Add(tgtnode, "Done");
        //
        //    AddEdge(currNodeStack.Peek(), tgtnode, label, (DateTime.Now - startTime).TotalSeconds);
        //    startTime = DateTime.Now;
        //}

        public double ComputeTimes(int nthreads)
        {
            // First, construct the time graph properly with times on nodes (not edges)
            Dictionary<int, double> nodeToTime = new Dictionary<int, double>();
            var nodeToChildren = new Dictionary<int, HashSet<int>>();

            // Nodes
            foreach (var tup in Edges)
                foreach (var e in tup.Value)
                    nodeToTime.Add(e.Item1, e.Item3);

            //nodeToTime.Keys.Iter(n => nodeToChildren.Add(n, new HashSet<int>()));
            foreach (var n in nodeToTime.Keys)
                nodeToChildren.Add(n, new HashSet<int>());

            // Edges
            foreach (var tup in Edges)
                foreach (var e in tup.Value)
                {
                    var n1 = tup.Key;
                    var n2 = e.Item1;
                    if (!nodeToTime.ContainsKey(n1) || !nodeToTime.ContainsKey(n2))
                        continue;
                    nodeToChildren[n1].Add(n2);
                }

            var rand = new Random((int)DateTime.Now.Ticks);

            var root = Edges[0].First().Item1;
            var isZero = new Func<double, bool>(d => d < 0.00001);
            var isNull = new Func<Tuple<int, double>, bool>(t => t.Item1 < 0);

            var threads = new Tuple<int, double>[nthreads];
            for (int i = 0; i < nthreads; i++)
                threads[i] = Tuple.Create(-1, 0.0);

            var totaltime = 0.0;
            var available = new List<int>();
            available.Add(root);

            while (true)
            {
                // Allocate to idle threads
                for (int i = 0; i < nthreads; i++)
                {
                    if (!isNull(threads[i])) continue;
                    if (!available.Any()) continue;
                    var index = rand.Next(available.Count);
                    threads[i] = Tuple.Create(available[index], nodeToTime[available[index]]);
                    available.RemoveAt(index);
                }
                if (threads.All(t => isNull(t))) break;

                // Run
                var min = threads.Where(t => !isNull(t)).Min(t => t.Item2);
                totaltime += min;
                for (int i = 0; i < nthreads; i++)
                {
                    if (isNull(threads[i])) continue;
                    var tleft = threads[i].Item2 - min;
                    if (isZero(tleft))
                    {
                        //nodeToChildren[threads[i].Item1].Iter(n => available.Add(n));
                        foreach (var n in nodeToChildren[threads[i].Item1])
                            available.Add(n);
                        threads[i] = Tuple.Create(-1, 0.0);
                    }
                    else
                        threads[i] = Tuple.Create(threads[i].Item1, tleft);
                }
            }

            return totaltime;
        }
    }
}
