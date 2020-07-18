using System;
using System.Collections.Generic;
using System.Linq;
using ThinkingWiresEnum;
using UnityEngine;

namespace ThinkingWires
{
    public sealed class FlowChartNode
    {
        public string Name;
        public int Index;
        public string[] ResultNames;
        public Dictionary<int, FlowChartNode> NextConditions = new Dictionary<int, FlowChartNode>();
        public Func<thinkingWiresColors[], int> Function;
        public int[] NextIndices;
        public thinkingWiresColors? BoxColor;
        public int? WireToCut;

        public FlowChartNode(string name, int index, string[] resultNames = null,  int[] nextIndices = null, Func<thinkingWiresColors[], int> function = null, thinkingWiresColors? boxColor = null, int? wireToCut = null)
        {
            Name = name;
            Index = index;
            ResultNames = resultNames;
            NextIndices = nextIndices;
            Function = function == null ? null : (Func<thinkingWiresColors[], int>) function.Clone();
            BoxColor = boxColor;
            WireToCut = wireToCut;
        }
        public void Validate()
        {
            if (ResultNames == null && NextIndices == null) return;
            if (ResultNames.Length != NextIndices.Length || new object[] { ResultNames, NextIndices }.Count(array => array == null) == 1)
                throw new Exception("Invalid Array Length: ResultNames and NextIndices do not have the same length.");
        }
        public int? Evaluate(thinkingWiresColors[] inputs, List<FlowChartNode> visited, string logging)
        {
            if (NextIndices == null) return WireToCut;
            int result = Function(inputs);
            if (BoxColor != null)
                visited.Add(this);
            if (!Enumerable.Range(0, NextIndices.Length).Contains(result))
                throw new Exception("Invalid Return Value: Return value must be non-negative integer less than length of NextIndices array.");
            Debug.LogFormat(logging, Name, ResultNames[result]);
            return NextConditions[NextIndices[result]].Evaluate(inputs, visited, logging);
        }
    }

    public sealed class FlowChart
    {
        public List<FlowChartNode> FlowChartGraph;
        public FlowChart(List<FlowChartNode> flowChartGraph)
        {
            FlowChartGraph = flowChartGraph;
            for (int i = 0; i < FlowChartGraph.Count; i++)
                for (int j = i + 1; j < FlowChartGraph.Count; j++)
                {
                    if (FlowChartGraph[i].NextIndices != null && FlowChartGraph[i].NextIndices.Contains(FlowChartGraph[j].Index))
                        FlowChartGraph[i].NextConditions.Add(FlowChartGraph[j].Index, FlowChartGraph[j]);
                    else if (FlowChartGraph[j].NextIndices != null && FlowChartGraph[j].NextIndices.Contains(FlowChartGraph[i].Index))
                        FlowChartGraph[j].NextConditions.Add(FlowChartGraph[i].Index, FlowChartGraph[i]);
                }
        }

        public int? EvaluateFromIndex(thinkingWiresColors[] inputs, List<FlowChartNode> visited, int startIndex, string logging)
        {
            for (int i = 0; i < FlowChartGraph.Count; i++)
                if (startIndex == FlowChartGraph[i].Index)
                {
                    FlowChartGraph[i].Validate();
                    return FlowChartGraph[i].Evaluate(inputs, visited, logging);
                }
            throw new Exception("Invalid Index Value: startIndex does not match any index in the graph.");
        }
    }
}
