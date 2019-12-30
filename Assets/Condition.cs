using System;

namespace ThinkingWires
{
    sealed class Condition
    {
        public string ConditionName { get; private set; }
        public string[] ConditionResultsName { get; private set; }
        public string BoxColor { get; private set; }
        public int BoxIndex { get; private set; }
        public int[] NextPossibleBoxes { get; private set; }
        public int NextBox { get; private set; }
        public Func<string[], int> Check { get; private set; }
        public string LogMessage { get; private set; }
        public Condition(string conditionName, string[] conditionsResultName, string boxColor, int boxIndex, int[] nextPossibleBoxes, Func<string[], int> check)
        {
            ConditionName = conditionName;
            ConditionResultsName = conditionsResultName;
            BoxColor = boxColor;
            BoxIndex = boxIndex;
            NextPossibleBoxes = nextPossibleBoxes;
            Check = check;
        }
        public void setWires(string[] wires)
        {
            Wires = wires;
            ConditionValue = Check(Wires);
            LogMessage = (ConditionName ?? "") + (ConditionResultsName == null ? "" : (": " + ConditionResultsName[ConditionValue]));
            NextBox = NextPossibleBoxes[ConditionValue];
        }
        private string[] Wires;
        private int ConditionValue { get; set; }
    }
}
