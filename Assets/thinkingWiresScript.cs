using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;
using ThinkingWires;
using Utilities;
using tEnum = ThinkingWiresEnum.thinkingWiresColors;

public class thinkingWiresScript : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombModule Module;
    public KMColorblindMode ColorblindMode;
    public KMSelectable ModuleSelectable;
    public KMSelectable Placeholder;
    public MeshRenderer[] LeftDigit;
    public MeshRenderer[] RightDigit;
    public GameObject[] UncutWires;
    public GameObject[] CutWires;
    public Animator WiresDoorAnimator;
    public Material[] Colors;
    public Material[] DisplayColor;
    public Transform Door;
    public TextMesh[] ColorblindTexts;

    private GameObject[][] _allWires;
    private tEnum[] _wiresColors;
    private List<FlowChartNode> _visitedNode = new List<FlowChartNode>();
    private static readonly string[][] _responses = new string[2][]
    {
        new string[2] { "Yes", "No" },
        new string[2] { "True", "False" }
    };
    private FlowChart _conditions;

    private int _displayNumber;
    private tEnum? _secondWireToCutEnum;
    private bool _secondStage = false;
    private bool _colorblind = false;

    //Souvenir Support
    private int firstWireToCut;
    private string secondWireToCut;
    #pragma warning disable 414
    private string screenNumber;
    #pragma warning restore 414

    //Logging
    static int moduleIdCounter = 1;
    int _moduleId;
    private bool moduleSolved;

    private void Start()
    {
        _colorblind = ColorblindMode.ColorblindModeActive;
        InitFlowChart();
        _moduleId = moduleIdCounter++;
        _allWires = UncutWires.SelectMany((wire, index) => CutWires.Where((_, index2) => index == index2).Select(wire2 => new[] { wire, wire2 })).ToArray();
        foreach (GameObject[] wires in _allWires)
        {
            GameObject[] wire = wires;
            wires[0].GetComponent<KMSelectable>().OnInteract += delegate { StartCoroutine(CutWire(wire)); return false; };
        }

        Module.OnActivate += delegate { StartCoroutine(DoorAnimation(close: false)); };
    }
    private IEnumerator CutWire(GameObject[] wires)
    {
        if (!wires[0].activeInHierarchy || ModuleSelectable.Children[0] == Placeholder) yield break;
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSnip, wires[0].transform);
        wires[0].SetActive(false);
        wires[1].SetActive(true);
        ModuleSelectable.Children[Array.IndexOf(_allWires, wires)] = null;
        ModuleSelectable.UpdateChildren();
        if (WiresDoorAnimator.GetBool("Transitioning"))
            yield break;
        WiresDoorAnimator.SetBool("Transitioning", true);
        if (!_secondStage && Array.IndexOf(_allWires, wires) + 1 == firstWireToCut)
        {
            Debug.LogFormat("[Thinking Wires #{0}] Wire {1} was cut which is correct. Progressing to the second stage.", _moduleId, firstWireToCut);
            _secondStage = true;
            StartCoroutine(DoorAnimation());
        }
        else if (_secondStage && (_secondWireToCutEnum == null || _wiresColors[Array.IndexOf(_allWires, wires)] == _secondWireToCutEnum))
        {
            if (_secondWireToCutEnum == null)
                Debug.LogFormat("[Thinking Wires #{0}] A wire was cut which is always valid. Solving the module.", _moduleId);
            else
                Debug.LogFormat("[Thinking Wires #{0}] A {1} wire was cut which is the correct color. Solving the module.", _moduleId, _secondWireToCutEnum.ToString().ToLowerInvariant());
            yield return StartCoroutine(DoorAnimation(open: false));
            SetDisplay(setGreen: true);
            moduleSolved = true;
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
            Module.HandlePass();
        }
        else
        {
            if (!_secondStage)
                Debug.LogFormat("[Thinking Wires #{0}] Wire {1} was cut when wire {2} must be cut during stage 1. Resetting the module and initiating a strike.", _moduleId, Array.IndexOf(_allWires, wires) + 1, firstWireToCut);
            else
                Debug.LogFormat("[Thinking Wires #{0}] {1} wire was cut when any {2} wire must be cut during stage 2. Resetting the module and initiating a strike.", _moduleId, _wiresColors[Array.IndexOf(_allWires, wires)].ToString(), secondWireToCut.ToLowerInvariant());
            Module.HandleStrike();
            _secondStage = false;
            _visitedNode.Clear();
            StartCoroutine(DoorAnimation());
        }
    }
    private IEnumerator DoorAnimation(bool open = true, bool close = true)
    {
        if (close)
        {
            yield return new WaitForSeconds(1);
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSequenceMechanism, Door);
            ModuleSelectable.Children = new[] { Placeholder };
            ModuleSelectable.UpdateChildren();
            WiresDoorAnimator.Play("Door Closing");
            foreach (GameObject[] go in _allWires)
                go[0].transform.GetChild(0).gameObject.SetActive(false);
            if (_colorblind)
                foreach (TextMesh tx in ColorblindTexts)
                    tx.gameObject.SetActive(false);
            yield return new WaitUntil(() => WiresDoorAnimator.GetCurrentAnimatorStateInfo(0).IsName("Door Closed"));
        }
        if (open)
        {
            Generate();
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSequenceMechanism, Door);
            WiresDoorAnimator.Play("Door Opening");
            yield return new WaitUntil(() => WiresDoorAnimator.GetCurrentAnimatorStateInfo(0).IsName("Door Opened"));
            foreach (GameObject[] go in _allWires)
                go[0].transform.GetChild(0).gameObject.SetActive(true);
            ModuleSelectable.Children = _allWires.Select(go => go[0].GetComponent<KMSelectable>()).ToArray();
            ModuleSelectable.UpdateChildren();
            if (_colorblind)
                foreach (TextMesh tx in ColorblindTexts)
                    tx.gameObject.SetActive(true);
        }
    }
    private void InitFlowChart()
    {
        _conditions = new FlowChart(new List<FlowChartNode>()
        {
            //string name, int index, string[] resultNames,  int[] nextIndices, Func<thinkingWiresColors[], int> function, thinkingWiresColors? boxColor = null, thinkingWiresColors? wireToCut = null
            //Red = 0, Green = 1, Blue = 2, White = 3
            new FlowChartNode("Start", 0, new[] { "<Evaluating at the next index>" }, new[] { 1 }, wires => 0),
            new FlowChartNode("Number of primary wires", 1, new[] { "0-2", "3+" }, new[] { 2, 4 }, wires => wires.Count(wire => new[] { tEnum.Red, tEnum.Green, tEnum.Blue }.Contains(wire)) <= 2 ? 0 : 1, tEnum.Red),
            new FlowChartNode("5th wire is white or black", 2, _responses[0], new[] { 12, 3 }, wires =>  new [] { tEnum.White, tEnum.Black }.Contains(wires[4]) ? 0 : 1, tEnum.Blue),
            new FlowChartNode("2 adjacent wires' colors are complementary", 3, _responses[1], new[] { 11, 5 }, wires => wires.SelectTwo((wire1, wire2) => new[] { wire1, wire2 })
                                                                                                                                         .Any(pair => (pair.Contains(tEnum.Red) && pair.Contains(tEnum.Cyan))||
                                                                                                                                                      (pair.Contains(tEnum.Green) && pair.Contains(tEnum.Magenta)) ||
                                                                                                                                                      (pair.Contains(tEnum.Blue) && pair.Contains(tEnum.Yellow)) ||
                                                                                                                                                      (pair.Contains(tEnum.White) && pair.Contains(tEnum.Black))) ? 0 : 1, tEnum.Green),
            new FlowChartNode("7th wire's color is secondary", 4, _responses[0], new[] { 3, 5 }, wires => new[] { tEnum.Cyan, tEnum.Magenta, tEnum.Yellow }.Contains(wires[6]) ? 0 : 1, tEnum.Yellow),
            new FlowChartNode("No wires are blue", 5,  _responses[1], new[] { 9, 6 }, wires => !wires.Contains(tEnum.Blue) ? 0 : 1, tEnum.Cyan),
            new FlowChartNode("5 or less wires' colors are present", 6, _responses[0], new[] { 8, 7 }, wires => wires.Distinct().Count() <= 5 ? 0 : 1, tEnum.Blue),
            new FlowChartNode("Blue wire is present", 7, _responses[0], new[] { 8, 10 }, wires => wires.Contains(tEnum.Blue) ? 0 : 1, tEnum.Green),
            new FlowChartNode("6th wire's color is not the same as one of the previous steps' color", 8, _responses[0], new[] { 19, 27 }, wires => !_visitedNode.Select(node => node.BoxColor).Contains(wires[5]) ? 0 : 1, tEnum.Magenta),
            new FlowChartNode("3rd wire's color is not black, blue, or yellow", 9, _responses[0], new[] { 7, 10 }, wires => !new[] { tEnum.Black, tEnum.Blue, tEnum.Yellow }.Contains(wires[2]) ? 0 : 1, tEnum.Red),
            new FlowChartNode("First 4 wires' colors are all different", 10, _responses[0], new[] { 19, 15 }, wires => wires.Take(4).Distinct().Count() == 4 ? 0 : 1, tEnum.Yellow),
            new FlowChartNode("2nd wire's color is white, black, or red", 11, _responses[0], new[] { 10, 14 }, wires => new[] { tEnum.White, tEnum.Black, tEnum.Red }.Contains(wires[1]) ? 0 : 1, tEnum.Black),
            new FlowChartNode("No wires are yellow", 12, _responses[1], new[] { 13, 11 }, wires => !wires.Contains(tEnum.Yellow) ? 0 : 1 , tEnum.Green),
            new FlowChartNode("1st wire's color is blue, cyan, or green", 13, _responses[0], new[] { 14, 16 }, wires => new[] { tEnum.Blue, tEnum.Cyan, tEnum.Green }.Contains(wires[0]) ? 0 : 1, tEnum.Cyan),
            new FlowChartNode("2 adjacent wires have the same color", 14, new[] { "<Dummy Condition>" }, new[] { 17 }, wires => 0, tEnum.White),
            new FlowChartNode("Most common wires' color groups", 15, new[] {"Primary", "Secondary", "Grayscale", "Tie" }, new[] { 18, 19, 24, 14 }, wires => {
                                                                                                                                                                 int[] colorCounts = new int[3]
                                                                                                                                                                 {
                                                                                                                                                                     wires.Count(wire => new[] { tEnum.Red, tEnum.Green, tEnum.Blue }.Contains(wire)),
                                                                                                                                                                     wires.Count(wire => new[] { tEnum.Cyan, tEnum.Magenta, tEnum.Yellow }.Contains(wire)),
                                                                                                                                                                     wires.Count(wire => new[] { tEnum.White, tEnum.Black }.Contains(wire))
                                                                                                                                                                 };
                                                                                                                                                                 return colorCounts.Count(ct => ct == colorCounts.Max()) == 1 ? Array.IndexOf(colorCounts, colorCounts.Max()) : 3;
                                                                                                                                                             }, tEnum.Black),
            new FlowChartNode("4th wire's color is the same as one of the previous steps' color", 16, _responses[0], new[] { 20, 17 }, wires => _visitedNode.Select(node => node.BoxColor).Contains(wires[3]) ? 0 : 1, tEnum.Magenta),
            new FlowChartNode("There is a secondary colored wire adjacent to both of its primary colors", 17, _responses[1], new[] { 18, 22 }, wires => {
                                                                                                                                                             var complementary = new Dictionary<tEnum, tEnum[]>()
                                                                                                                                                             {
                                                                                                                                                                 { tEnum.Cyan, new tEnum[] { tEnum.Green, tEnum.Blue } },
                                                                                                                                                                 { tEnum.Magenta, new tEnum[] { tEnum.Red, tEnum.Blue } },
                                                                                                                                                                 { tEnum.Yellow, new tEnum[] { tEnum.Red, tEnum.Green } }
                                                                                                                                                             };
                                                                                                                                                             return wires.SelectThree((wire1, wire2, wire3) => new[] { wire1, wire2, wire3 })
                                                                                                                                                                         .Where(triplet => new[] { tEnum.Cyan, tEnum.Magenta, tEnum.Yellow }.Contains(triplet[1]) && triplet[0] != triplet[2])
                                                                                                                                                                         .Any(triplet => complementary[triplet[1]].Contains(triplet[0]) && complementary[triplet[1]].Contains(triplet[2])) ? 0 : 1;
                                                                                                                                                         }, tEnum.Blue),
            new FlowChartNode("<Empty Box>", 18, new[] { "<Empty>" }, new[] { 23 }, wires => 0, tEnum.Cyan),
            new FlowChartNode("No wires are white or black", 19, _responses[1], new[] { 25, 26 }, wires => !wires.Contains(tEnum.White) && !wires.Contains(tEnum.Black) ? 0 : 1, tEnum.White),
            new FlowChartNode("Exactly 3 wires have the same color", 20, _responses[1], new[] { 22, 21 }, wires => wires.Any(color => wires.Where(x => x == color).Count() == 3) ? 0 : 1, tEnum.Yellow),
            new FlowChartNode("Cut 1st wire", 21, wireToCut: 1),
            new FlowChartNode("Cut 2nd wire", 22, wireToCut: 2),
            new FlowChartNode("Cut 3rd wire", 23, wireToCut: 3),
            new FlowChartNode("Cut 4th wire", 24, wireToCut: 4),
            new FlowChartNode("Cut 5th wire", 25, wireToCut: 5),
            new FlowChartNode("Cut 6th wire", 26, wireToCut: 6),
            new FlowChartNode("Cut 7th wire", 27, wireToCut: 7)
        });
    }
    private void Generate()
    {
        int tries = 0;
        tryagain:
        _wiresColors = Enumerable.Range(0, 7).Select(x => (tEnum) Rnd.Range(0, 8)).ToArray();
        if (!_secondStage)
        {
            Debug.LogFormat("[Thinking Wires #{0}] In stage 1, wires' colors from top to bottom are the following: [{1}]", _moduleId, string.Join(", ", _wiresColors.Select(t => t.ToString()).ToArray()));
            firstWireToCut = (int)_conditions.EvaluateFromIndex(_wiresColors, _visitedNode, 0, string.Format("[Thinking Wires #{0}] {{0}}: {{1}}", _moduleId));
            SetDisplay(setOff: true);
            Debug.LogFormat("[Thinking Wires #{0}] The first wire to cut is wire {1}.", _moduleId, firstWireToCut);
        }
        else
        {
            _displayNumber = Rnd.Range(1, _visitedNode.Count + 1);
            if (!_wiresColors.Contains((tEnum)_visitedNode[_displayNumber - 1].BoxColor))
            {
                tries++;
                if (tries < 3)
                    goto tryagain;
                _displayNumber = 69;
                _secondWireToCutEnum = null;
            }
            else
                _secondWireToCutEnum = _visitedNode[_displayNumber - 1].BoxColor;
            screenNumber = _displayNumber.ToString("00");
            Debug.LogFormat("[Thinking Wires #{0}] In stage 2, wires' colors from top to bottom are the following: [{1}]", _moduleId, string.Join(", ", _wiresColors.Select(t => t.ToString()).ToArray()));
            Debug.LogFormat("[Thinking Wires #{0}] The display number is {1}.", _moduleId, _displayNumber);
            secondWireToCut = _secondWireToCutEnum == null ? "Any" : _secondWireToCutEnum.ToString();
            if (secondWireToCut == "Any")
                Debug.LogFormat("[Thinking Wires #{0}] The second wire to cut can have any color.", _moduleId);
            else
                Debug.LogFormat("[Thinking Wires #{0}] The second wire to cut must have {1} color.", _moduleId, secondWireToCut.ToLowerInvariant());
            SetDisplay();
        }
        foreach (TextMesh tx in ColorblindTexts)
            tx.text = _wiresColors[Array.IndexOf(ColorblindTexts, tx)] == tEnum.Black ? "K" : _wiresColors[Array.IndexOf(ColorblindTexts, tx)].ToString().Substring(0, 1);
        foreach (GameObject[] go in _allWires)
        {
            go[0].GetComponent<MeshRenderer>().material = Colors[(int)_wiresColors[Array.IndexOf(_allWires, go)]];
            go[0].SetActive(true);
            go[1].GetComponent<MeshRenderer>().material = Colors[(int)_wiresColors[Array.IndexOf(_allWires, go)]];
            go[1].SetActive(false);
        }
    }
    private void SetDisplay(bool setOff = false, bool setGreen = false)
    {
        if (setGreen)
        {
            foreach (MeshRenderer mr in LeftDigit)
                mr.material = DisplayColor[2];
            foreach (MeshRenderer mr in RightDigit)
                mr.material =  DisplayColor[2];
        }
        else if (setOff)
        {
            foreach (MeshRenderer mr in LeftDigit)
                mr.material = DisplayColor[0];
            foreach (MeshRenderer mr in RightDigit)
                mr.material = DisplayColor[0];
        }
        else
        {
            int firstNumber = _displayNumber / 10;
            int secondNumber = _displayNumber % 10;
            byte[] seven = new byte[10] { 63, 6, 91, 79, 102, 109, 125, 7, 127, 111 };
            BitArray firstBitArray = new BitArray(new byte[] { seven[firstNumber] });
            BitArray secondBitArray = new BitArray(new byte[] { seven[secondNumber] });
            foreach (MeshRenderer mr in LeftDigit)
                mr.material = firstBitArray[Array.IndexOf(LeftDigit, mr)] ? DisplayColor[1] : DisplayColor[0];
            foreach (MeshRenderer mr in RightDigit)
                mr.material = secondBitArray[Array.IndexOf(RightDigit, mr)] ? DisplayColor[1] : DisplayColor[0];
        }
    }

    //Twitch Plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = "Use !{0} cut 5 to cut any wire ranging from 1 to 7 where wire 1 is the topmost wire.\n Colo(u)rblind mode: Use !{0} <keyword> to activate colo(u)rblind where the possible keywords are colorblind, colourblind, colo(u)rblind, blind, color, colour, colo(u)r, Where are colors?, What colors?, Where are colours?, What colours?, Where are colo(u)rs?, What colo(u)rs?, I'm colorblind!, I'm colourblind!, I'm colo(u)rblind!, Color god please help me, Colour god please help me, Colo(u)r god please help me";
    #pragma warning restore 414

    private IEnumerator TwitchHandleForcedSolve()
    {
        while (true)
        {
            while (ModuleSelectable.Children[0] != Placeholder ? WiresDoorAnimator.GetBool("Transitioning") && !moduleSolved: !moduleSolved) yield return true;
            if (moduleSolved) yield break;
            if (!_secondStage)
            {
                _allWires[firstWireToCut - 1][0].GetComponent<KMSelectable>().OnInteract();
                yield return new WaitForSeconds(.1f);
            }
            else
            {
                if (_secondWireToCutEnum == null)
                    _allWires[Rnd.Range(0, 7)][0].GetComponent<KMSelectable>().OnInteract();
                else
                    _allWires[Array.IndexOf(_wiresColors, _secondWireToCutEnum)][0].GetComponent<KMSelectable>().OnInteract();
                yield return new WaitForSeconds(.1f);
            }
        }
    }
    private IEnumerator ProcessTwitchCommand(string cmd)
    {
        if (Regex.IsMatch(cmd, @"^\s*(((?=[cb])(colo(u|\(u\))?r)?(blind)?)|(I'm\s+colo(u|\(u\))?rblind!)|(((Where\s+are)|(What))\s+colo(u|\(u\))?rs\?)|(Colo(u|\(u\))?r\s+god\s+please\s+help\s+me))\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (_colorblind)
                yield break;
            _colorblind = true;
            if(ModuleSelectable.Children[0] == Placeholder)
                foreach (TextMesh tx in ColorblindTexts)
                    tx.gameObject.SetActive(true);
            yield break;
        }
        Match m;
        if ((m = Regex.Match(cmd, @"^cut\s+([1-7])$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)).Success)
        {
            int wireIndex = int.Parse(m.Groups[1].Value) - 1;
            if (_allWires[wireIndex][0].activeInHierarchy && ModuleSelectable.Children[0] == Placeholder)
            {
                yield return null;
                _allWires[wireIndex][0].GetComponent<KMSelectable>().OnInteract();
                yield return new WaitForSeconds(.1f);
                yield return "solve";
            }
            else
                yield return "sendtochaterror Unable to cut wire " + (wireIndex + 1).ToString() + ". Please make sure that the wire can be cut or active.";
        }
        else
            yield return "sendtochaterror Valid commands are cut and colorblind. Please use !{1} help to see full commands.";
    }
}
