using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;
using ThinkingWires;

public class thinkingWiresScript : MonoBehaviour
{

    public KMAudio audio;
    public KMBombModule module;
    public KMSelectable moduleSelectable;
    public KMColorblindMode ColorblindMode;

    public GameObject[] wiresObject;
    public GameObject[] sevenSegmentsObject;
    public GameObject door;
    public GameObject[] colorblindTexts;

    public Material[] wireColors;
    public Material[] sevenSegmentsColors;

    private KMSelectable[] wires = new KMSelectable[7];
    private string[] wireColorNames = new string[7];
    private string[] originalColorNames = new string[7];
    private int[] colorIndex = new int[7];
    private int randomColorIndex;
    private int currentBoxIndex;
    private int boxCounter;
    private static List<string> boxColor = new List<string>();
    private int firstWireToCut;
    private bool secondStage;
    private string secondWireToCut;
    private string screenNumber;
    private bool handlingStrike;
    private bool[] isCut = new bool[7];
    private bool colorblind = false;
   
    bool wireToCutFound = false; //A measure to prevent hanging the game from infinite while loop
    bool doorOpened = false;
    bool activated = false;
    //Logging
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    private static readonly string[] possibleColorsName = new string[] { "Red", "Green", "Blue", "White", "Cyan", "Magenta", "Yellow", "Black" };
    private static readonly List<HashSet<string>> colorGroup = new List<HashSet<string>>()
    {
        new HashSet<string> { "Red", "Green", "Blue" },
        new HashSet<string> { "Cyan", "Magenta", "Yellow" },
        new HashSet<string> { "White", "Black" }
    };
    private static readonly List<HashSet<HashSet<string>>> primarySecondaryGroup = new List<HashSet<HashSet<string>>>()
    {
        new HashSet<HashSet<string>> { new HashSet<string> { "Yellow" }, new HashSet<string> { "Red", "Green" } },
        new HashSet<HashSet<string>> { new HashSet<string> { "Magenta" }, new HashSet<string> { "Red", "Blue" } },
        new HashSet<HashSet<string>> { new HashSet<string> { "Cyan" }, new HashSet<string> { "Green", "Blue" } }
    };
    private static int HighestColorGroup(string[] wires)
    {
        var colorCounts = colorGroup.Select(colorSet => wires.Count(wiresColor => colorSet.Contains(wiresColor))); //Count the number of colors in each set and put into IEnumerable<int> object
        var maximum = colorCounts.Max(); //Find maximum value in IEnumberable<int> object
        var maximumIndices = colorCounts.Select((value, index) => value == maximum ? index : -1).Where(index => index != -1); //Put all indices of value that is equal to maximum
        return maximumIndices.Count() == 1 ? maximumIndices.ToArray()[0] : 3; //If there is a maximum tie, return index 3
    }
    private static int checkSecondaryNextToPrimaries(string[] wires)
    {
        var conditionFlag = Enumerable.Range(1, 5).Any(index => primarySecondaryGroup
        .Any(nestedSet => nestedSet
        .All(set => set.SetEquals(new HashSet<string> { wires[index] }) ||
                    set.SetEquals(new HashSet<string> { wires[index - 1], wires[index + 1] }))));
        return conditionFlag ? 0 : 1;
    }
    private static int checkTwoAdjacentComplementary(string[] wires)
    {
        var conditionFlag = Enumerable.Range(0, 6)
            .Any(index => wires[index] != wires[index + 1] &&
                (Array.IndexOf(possibleColorsName, wires[index]) - Array.IndexOf(possibleColorsName, wires[index + 1]) + 8) % 4 == 0) ? 0 : 1;
        return conditionFlag;
    }
    private Condition[] possibleConditions = new Condition[]
    {
                new Condition("Number of primary wires", new[] { "3+", "0 - 2" }, "Red", 0, new[] { 3, 1 }, wires => wires.Count(col => colorGroup[0].Contains(col)) >= 3 ? 0 : 1),
                new Condition("5th Wire white or black", new[] { "Yes", "No"}, "Blue", 1, new[] { 12, 2}, wires => colorGroup[2].Contains(wires[4]) ? 0 : 1),
                new Condition("2 adjacent wires are complementary", new[] { "True", "False" }, "Green", 2, new[] { 11, 4 }, wires => checkTwoAdjacentComplementary(wires)),
                new Condition("7th wire is secondary", new[] { "Yes", "No" }, "Yellow", 3, new[] { 2, 4 }, wires => colorGroup[1].Contains(wires[6]) ? 0 : 1),
                new Condition("No wires are blue", new[] { "True", "False" }, "Cyan", 4, new[] { 8, 5 }, wires => !wires.Contains("Blue") ? 0 : 1),
                new Condition("5 or less wire colors present", new[] { "Yes", "No" }, "Blue", 5, new[] { 7, 6 }, wires => wires.Distinct().Count() <= 5 ? 0 : 1),
                new Condition("Blue wire present", new[] { "Yes", "No" }, "Green", 6, new[] { 7, 10 }, wires => wires.Contains("Blue") ? 0 : 1),
                new Condition("One of the previous box color not the same as the 6th wire color", new[] { "Yes", "No" }, "Magenta", 7, new[] { 9, 26 }, wires => !boxColor.Contains(wires[5]) ? 0 : 1),
                new Condition("3rd wire not black, blue, or yellow", new[] { "Yes", "No" }, "Red", 8, new[] { 6, 10 }, wires => (wires[2] != "Black" && wires[2] != "Blue" && wires[2] != "Yellow") ? 0 : 1),
                new Condition("No wires are white or black", new[] { "True", "False" }, "White", 9, new[] { 24, 25 }, wires => !wires.Any(col => colorGroup[2].Contains(col)) ? 0 : 1),
                new Condition("First 4 wires all different colors", new[] { "True", "False" }, "Yellow", 10, new[] { 9, 15 }, wires => wires.Where((col, index) => index < 4).Distinct().Count() == 4 ? 0 : 1),
                new Condition("2nd wire white, black, or red", new[] { "Yes", "No" }, "Black", 11, new[] { 10, 14 }, wires => (wires[1] == "White" || wires[1] == "Black" || wires[1] == "Red") ? 0 : 1),
                new Condition("No wires are yellow", new[] { "True", "False" }, "Green", 12, new[] { 13, 11 }, wires => !wires.Contains("Yellow") ? 0 : 1),
                new Condition("1st wire blue, cyan, or green", new[] { "Yes", "No" }, "Cyan", 13, new[] { 14, 16 }, wires => (wires[0] == "Blue" || wires[0] == "Cyan" || wires[0] == "Green") ? 0 : 1),
                new Condition("2 adjacent wires are the same color (Dummy Rule)", null, "White", 14, new[] { 17 }, wires => 0),
                new Condition("Most common wire colors", new[] { "RGB", "CMY", "WK", "Tie"}, "Black", 15, new[] { 19, 9, 23, 14 }, wires => HighestColorGroup(wires)),
                new Condition("One of the previous box color the same as the 4th wire color", new[] { "Yes", "No" }, "Magenta", 16, new[] { 18, 17 }, wires => boxColor.Contains(wires[3]) ? 0 : 1),
                new Condition("Secondary color adjacent to both it's primary colors", new[] { "True", "False" }, "Blue", 17, new[] { 19, 21 }, wires => checkSecondaryNextToPrimaries(wires)),
                new Condition("Exactly 3 wires are the same color", new[] { "True", "False" }, "Yellow", 18, new[] { 21, 20 }, wires => wires.Any(color => wires.Where(x => x == color).Count() == 3) ? 0 : 1 ),
                new Condition("No condition to check. (An empty box)", null, "Cyan", 19, new[] { 22 }, wires => 0),
    };
    private void Start()
    {
        colorblind = ColorblindMode.ColorblindModeActive;
        moduleId = moduleIdCounter++;
        module.OnActivate += Activate;
        doorOpened = false;
        for (int index = 0; index < 7; index++)
        {
            var j = index;
            wires[j] = wiresObject[j].transform.Find("Wire " + (j + 1).ToString()).GetComponent<KMSelectable>();
            wires[j].OnInteract += delegate () { CutWires(j); return false; };
        }
        wiresObject[0].transform.localPosition += new Vector3(0, -0.011f, 0);
        wiresObject[1].transform.localPosition += new Vector3(0, -0.011f, 0);
        wiresObject[2].transform.localPosition += new Vector3(0, -0.011f, 0);
        wiresObject[3].transform.localPosition += new Vector3(0, -0.011f, 0);
        wiresObject[4].transform.localPosition += new Vector3(0, -0.011f, 0);
        wiresObject[5].transform.localPosition += new Vector3(0, -0.011f, 0);
        wiresObject[6].transform.localPosition += new Vector3(0, -0.011f, 0);
        for (int index = 0; index < 7; index++)
        {
            wires[index].enabled = false;
            wiresObject[index].transform.Find("Wire " + (index + 1).ToString() + " Highlight").gameObject.SetActive(false);
            if (index != 0)
                moduleSelectable.Children[index] = null;
        }
        moduleSelectable.UpdateChildren();
    }

	// Use this for initialization
	private void Activate()
    {
        StartCoroutine(Initialization());
    }

    private IEnumerator Initialization()
    {
        for (int sevenIndex = 0; sevenIndex < 2; sevenIndex++)
            for (int sevenSubIndex = 0; sevenSubIndex < 7; sevenSubIndex++)
                sevenSegmentsObject[sevenIndex].transform.Find("SevenSegmentsPiece " + (sevenSubIndex + 1).ToString()).GetComponent<Renderer>().material = sevenSegmentsColors[1];
        if (doorOpened)
        {
            yield return StartCoroutine(AnimatingDoor(true));
        }
        wireColorNames = new string[7];
        secondStage = false;
        currentBoxIndex = 0;
        wireToCutFound = false;
        boxCounter = 0;
        boxColor.Clear();
        for (int index = 0; index < 7; index++)
        {
            isCut[index] = false;
            wiresObject[index].transform.Find("Wire " + (index + 1).ToString()).gameObject.SetActive(true);
            wiresObject[index].transform.Find("CutWire " + (index + 1).ToString()).gameObject.SetActive(false);
        }
        SelectColors();
        GenerateAnswerStage1();
        yield return StartCoroutine(AnimatingDoor(false));
        activated = true;
        handlingStrike = false;
    }

    private void CutWires(int cutWireIndex)
    {
        if (!doorOpened)
            return;
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSnip, transform);
        isCut[cutWireIndex] = true;
        wires[cutWireIndex].enabled = false;
        wiresObject[cutWireIndex].transform.Find("Wire " + (cutWireIndex + 1).ToString()).gameObject.SetActive(false);
        wiresObject[cutWireIndex].transform.Find("Wire " + (cutWireIndex + 1).ToString() + " Highlight").gameObject.SetActive(false);
        wiresObject[cutWireIndex].transform.Find("CutWire " + (cutWireIndex + 1).ToString()).gameObject.SetActive(true);
        foreach (Renderer r in wiresObject[cutWireIndex].GetComponentsInChildren<Renderer>())
        {
            r.material = wireColors[colorIndex[cutWireIndex]];
        }
        moduleSelectable.Children[cutWireIndex] = null;
        moduleSelectable.UpdateChildren();

        if (moduleSolved || handlingStrike)
            return;

        if (!secondStage)
        {
            if ((cutWireIndex + 1) == firstWireToCut)
            {
                wireColorNames = wireColorNames.Where(w => w != wireColorNames[cutWireIndex]).ToArray();
                secondStage = true;
                Debug.LogFormat("[Thinking Wires #{0}] The {1} wire was cut which matches with the correct answer. Advancing to second stage.", moduleId, cutWireIndex + 1);
                GenerateAnswerStage2();
                SevenSegmentsDisplay();
            }
            else
            {
                Debug.LogFormat("[Thinking Wires #{0}] Wire {1} was cut while expecting wire {2}. Strike! Regenerating the module...", moduleId, cutWireIndex + 1, firstWireToCut);
                handlingStrike = true;
                StartCoroutine(Strike());
            }
        }
        else
        {
            if (originalColorNames[cutWireIndex] == secondWireToCut || secondWireToCut == "Any")
            {
                Debug.LogFormat("[Thinking Wires #{0}] The correct wire has been cut. Module solved!", moduleId);
                moduleSolved = true;
                StartCoroutine(Solve());
            }
            else
            {
                Debug.LogFormat("[Thinking Wires #{0}] {1} Wire was cut while expecting {2} wire. Strike! Regenerating the module...", moduleId, originalColorNames[cutWireIndex], secondWireToCut);
                handlingStrike = true;
                StartCoroutine(Strike());
            }
        }
    }
	
    private IEnumerator SetSelectables(bool notEmpty)
    {
        List<KMSelectable> newSelectables = new List<KMSelectable>();
        if (notEmpty)
        {
            for (int index = 0; index < 7; index++)
            {
                newSelectables.Add(wires[index]);
            }
            moduleSelectable.Children = newSelectables.ToArray();
        }
        else
        {
            for (int index = 1; index < 7; index++)
                moduleSelectable.Children[index] = null;
        }
        moduleSelectable.Children[0] = wires[0]; //Serves as a dummy selectable to ensure that the gamepad can be used to unview the module
        yield return null;
        moduleSelectable.UpdateChildren();
    }

    private IEnumerator Strike()
    {
        module.HandleStrike();
        yield return new WaitForSeconds(1f);
        Activate();
    }

    private IEnumerator AnimatingDoor(bool isOpen)
    {
        GetComponent<KMAudio>().PlayGameSoundAtTransformWithRef(KMSoundOverride.SoundEffect.WireSequenceMechanism, transform);
        if (isOpen)
        {
            yield return StartCoroutine(SetSelectables(false));
            for (int index = 0; index < 7; index++)
            {
                wires[index].enabled = false;
                wiresObject[index].transform.Find("Wire " + (index + 1).ToString() + " Highlight").gameObject.SetActive(false);
            }
            if (colorblind)
            {
                for (int index = 0; index < 7; index++)
                {
                    colorblindTexts[index].gameObject.SetActive(false);
                }
            }
            for (int i = 0; i < 11; i++)
            {
                wiresObject[0].transform.localPosition += new Vector3(0, -0.001f, 0);
                wiresObject[1].transform.localPosition += new Vector3(0, -0.001f, 0);
                wiresObject[2].transform.localPosition += new Vector3(0, -0.001f, 0);
                wiresObject[3].transform.localPosition += new Vector3(0, -0.001f, 0);
                wiresObject[4].transform.localPosition += new Vector3(0, -0.001f, 0);
                wiresObject[5].transform.localPosition += new Vector3(0, -0.001f, 0);
                wiresObject[6].transform.localPosition += new Vector3(0, -0.001f, 0);
                yield return new WaitForSeconds(0.01F);
            }
            for (int i = 0; i < 44; i++)
            {
                door.transform.localScale += new Vector3(1, 0, 0);
                yield return new WaitForSeconds(0.005f);
            }
            yield return new WaitForSeconds(0.5f);
            doorOpened = false;
        }
        else
        {
            for (int i = 0; i < 44; i++)
            {
                door.transform.localScale -= new Vector3(1, 0, 0);
                yield return new WaitForSeconds(0.005F);
            }
            if (colorblind)
            {
                for (int index = 0; index < 7; index++)
                {
                    colorblindTexts[index].gameObject.SetActive(true);
                }
            }
            for (int i = 0; i < 11; i++)
            {
                wiresObject[0].transform.localPosition += new Vector3(0, 0.001f, 0);
                wiresObject[1].transform.localPosition += new Vector3(0, 0.001f, 0);
                wiresObject[2].transform.localPosition += new Vector3(0, 0.001f, 0);
                wiresObject[3].transform.localPosition += new Vector3(0, 0.001f, 0);
                wiresObject[4].transform.localPosition += new Vector3(0, 0.001f, 0);
                wiresObject[5].transform.localPosition += new Vector3(0, 0.001f, 0);
                wiresObject[6].transform.localPosition += new Vector3(0, 0.001f, 0);
                yield return new WaitForSeconds(0.01F);
            }
            for (int index = 0; index < 7; index++)
            {
                wires[index].enabled = true;
                wiresObject[index].transform.Find("Wire " + (index + 1).ToString() + " Highlight").gameObject.SetActive(true);
            }
            yield return StartCoroutine(SetSelectables(true));
            doorOpened = true;
        }
    }

    private IEnumerator Solve()
    {
        module.HandlePass();
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
        for (int sevenIndex = 0; sevenIndex < 2; sevenIndex++)
            for (int sevenSubIndex = 0; sevenSubIndex < 7; sevenSubIndex++)
                sevenSegmentsObject[sevenIndex].transform.Find("SevenSegmentsPiece " + (sevenSubIndex + 1).ToString()).GetComponent<Renderer>().material = sevenSegmentsColors[2];
        yield return new WaitForSeconds(1.5f);
        yield return (AnimatingDoor(true));
        for (int index = 0; index < 7; index++)
        {
            wires[index].enabled = false;
            wiresObject[index].transform.Find("Wire " + (index + 1).ToString() + " Highlight").gameObject.SetActive(false);
        }
    }

    void SelectColors()
    {
        //Test code
        //Red = 0, Green = 1, Blue = 2, White = 6, Cyan = 3, Magenta = 4, Yellow = 5, Black = 7
        //int[] testColors = new int[7] {4, 0, 4, 4, 1, 6, 7};
        for (int index = 0; index < 7; index++)
        {
            randomColorIndex = Rnd.Range(0, 8);
            //Test Code
            //randomColorIndex = testColors[index];
            foreach (Renderer r in wiresObject[index].GetComponentsInChildren<Renderer>())
            {
                r.material = wireColors[randomColorIndex];
                colorIndex[index] = randomColorIndex;
            }
            if (wireColors[randomColorIndex].name.Length > 1)
            {
                wireColorNames[index] = char.ToUpperInvariant(wireColors[randomColorIndex].name[0]) + wireColors[randomColorIndex].name.Substring(1);
                originalColorNames[index] = char.ToUpperInvariant(wireColors[randomColorIndex].name[0]) + wireColors[randomColorIndex].name.Substring(1);
            }
            else
            {
                wireColorNames[index] = wireColors[randomColorIndex].name.ToUpperInvariant();
                originalColorNames[index] = wireColors[randomColorIndex].name.ToUpperInvariant();
            }
            if (originalColorNames[index] == "Black")
            {
                colorblindTexts[index].GetComponent<TextMesh>().text = "K";
            }
            else
            {
                colorblindTexts[index].GetComponent<TextMesh>().text = originalColorNames[index][0].ToString();
            }
        }
        Debug.LogFormat("[Thinking Wires #{0}] Wires colors from top to bottom: [{1}]", moduleId, wireColorNames.Join(", "));
    }

    void GenerateAnswerStage1()
    {
        while (!wireToCutFound && boxCounter < 15 && wireColorNames.Length == 7)
        {
            if (currentBoxIndex > 19)
            {
                firstWireToCut = currentBoxIndex - 19;
                wireToCutFound = true;
                break;
            }
            possibleConditions[currentBoxIndex].setWires(wireColorNames);
            Debug.LogFormat("[Thinking Wires #{0}] {1}", moduleId, possibleConditions[currentBoxIndex].LogMessage);
            boxColor.Add(possibleConditions[currentBoxIndex].BoxColor);
            currentBoxIndex = possibleConditions[currentBoxIndex].NextBox;
            boxCounter++;
        }
        Debug.LogFormat("[Thinking Wires #{0}] The first wire that is needed to be cut is wire {1}.", moduleId, firstWireToCut);
        if (!wireToCutFound)
        {
            Debug.LogFormat("[Thinking Wires #{0}] Couldn't generate the correct wire to cut. Please report this bug to the current maintainer.", moduleId);
        }
    }

    void GenerateAnswerStage2 ()
    {
        if(!wireColorNames.Any(color => boxColor.Contains(color)))
        {
            secondWireToCut = "Any";
            screenNumber = "69";
        }
        else
        {
            int subArrayIndex = 0;
            string[][] nameAndIndex = new string [boxColor.Count(names => wireColorNames.Contains(names))][];
            for (int index = 0; index < boxColor.Count(); index++)
            {
                if (wireColorNames.Contains(boxColor[index]))
                {
                    nameAndIndex[subArrayIndex] = new string[2];
                    nameAndIndex[subArrayIndex][0] = boxColor[index];
                    nameAndIndex[subArrayIndex][1] = (index + 1).ToString();
                    subArrayIndex++;
                }
            }
            int randomizedIndex = Rnd.Range(0, nameAndIndex.Length);
            secondWireToCut = nameAndIndex[randomizedIndex][0];
            if (nameAndIndex[randomizedIndex][1].Length == 1)
                screenNumber = "0" + nameAndIndex[randomizedIndex][1];
            else
                screenNumber = nameAndIndex[randomizedIndex][1];
        }
        Debug.LogFormat("[Thinking Wires #{0}] The display number is {1}.", moduleId, screenNumber);
        Debug.LogFormat("[Thinking Wires #{0}] The second wire that is needed to be cut is {1} wire.", moduleId, secondWireToCut);
    }

    void SevenSegmentsDisplay()
    {
        bool[][] sevenSegmentFlag = new bool[2][];
        sevenSegmentFlag[0] = new bool[7];
        sevenSegmentFlag[1] = new bool[7];

        for (int index = 0; index < 2; index++)
        {
            // Initialize the array
            for (int subIndex = 0; subIndex < 7; subIndex++)
            {
                sevenSegmentFlag[index][subIndex] = false;
            }

            switch (screenNumber[index])
            {
                case '0':
                    sevenSegmentFlag[index][0] = true;
                    sevenSegmentFlag[index][1] = true;
                    sevenSegmentFlag[index][2] = true;
                    sevenSegmentFlag[index][3] = true;
                    sevenSegmentFlag[index][4] = true;
                    sevenSegmentFlag[index][5] = true;
                    break;
                case '1':
                    sevenSegmentFlag[index][1] = true;
                    sevenSegmentFlag[index][2] = true;
                    break;
                case '2':
                    sevenSegmentFlag[index][0] = true;
                    sevenSegmentFlag[index][1] = true;
                    sevenSegmentFlag[index][3] = true;
                    sevenSegmentFlag[index][4] = true;
                    sevenSegmentFlag[index][6] = true;
                    break;
                case '3':
                    sevenSegmentFlag[index][0] = true;
                    sevenSegmentFlag[index][1] = true;
                    sevenSegmentFlag[index][2] = true;
                    sevenSegmentFlag[index][3] = true;
                    sevenSegmentFlag[index][6] = true;
                    break;
                case '4':
                    sevenSegmentFlag[index][1] = true;
                    sevenSegmentFlag[index][2] = true;
                    sevenSegmentFlag[index][5] = true;
                    sevenSegmentFlag[index][6] = true;
                    break;
                case '5':
                    sevenSegmentFlag[index][0] = true;
                    sevenSegmentFlag[index][2] = true;
                    sevenSegmentFlag[index][3] = true;
                    sevenSegmentFlag[index][5] = true;
                    sevenSegmentFlag[index][6] = true;
                    break;
                case '6':
                    sevenSegmentFlag[index][0] = true;
                    sevenSegmentFlag[index][2] = true;
                    sevenSegmentFlag[index][3] = true;
                    sevenSegmentFlag[index][4] = true;
                    sevenSegmentFlag[index][5] = true;
                    sevenSegmentFlag[index][6] = true;
                    break;
                case '7':
                    sevenSegmentFlag[index][0] = true;
                    sevenSegmentFlag[index][1] = true;
                    sevenSegmentFlag[index][2] = true;
                    break;
                case '8':
                    sevenSegmentFlag[index][0] = true;
                    sevenSegmentFlag[index][1] = true;
                    sevenSegmentFlag[index][2] = true;
                    sevenSegmentFlag[index][3] = true;
                    sevenSegmentFlag[index][4] = true;
                    sevenSegmentFlag[index][5] = true;
                    sevenSegmentFlag[index][6] = true;
                    break;
                case '9':
                    sevenSegmentFlag[index][0] = true;
                    sevenSegmentFlag[index][1] = true;
                    sevenSegmentFlag[index][2] = true;
                    sevenSegmentFlag[index][3] = true;
                    sevenSegmentFlag[index][5] = true;
                    sevenSegmentFlag[index][6] = true;
                    break;
            }
            for (int subIndex = 0; subIndex < 7; subIndex++)
            {
                if (sevenSegmentFlag[index][subIndex])
                {
                    sevenSegmentsObject[index].transform.Find("SevenSegmentsPiece " + (subIndex + 1).ToString()).GetComponent<Renderer>().material = sevenSegmentsColors[0];
                }
                else
                {
                    sevenSegmentsObject[index].transform.Find("SevenSegmentsPiece " + (subIndex + 1).ToString()).GetComponent<Renderer>().material = sevenSegmentsColors[1];
                }
            }
        }
    }

    //Twitch Plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = "Use !{0} cut 5 to cut any wire ranging from 1 to 7 where wire 1 is the topmost wire.\n Colo(u)rblind mode: Use !{0} <keyword> to activate colo(u)rblind where the possible keywords are colorblind, colourblind, colo(u)rblind, blind, color, colour, colo(u)r, Where are colors?, What colors?, Where are colours?, What colours?, Where are colo(u)rs?, What colo(u)rs?, I'm colorblind!, I'm colourblind!, I'm colo(u)rblind!, Color god please help me, Colour god please help me, Colo(u)r god please help me";
    #pragma warning restore 414

    public IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
        while (!moduleSolved)
        {
            yield return new WaitForSeconds(0.1f);
            if (!handlingStrike && doorOpened)
            {
                if (!secondStage)
                {
                    wires[firstWireToCut - 1].OnInteract();
                    yield return new WaitForSeconds(0.2f);
                }
                for (int index = 0; index < 7; index++)
                {
                    if (!isCut[index] && originalColorNames[index] == secondWireToCut)
                    {
                        wires[index].OnInteract();
                        yield return new WaitForSeconds(0.2f);
                        break;
                    }
                }
            }
        }
    }

    public IEnumerator ProcessTwitchCommand(string command)
    {
        if (!activated || handlingStrike)
        {
            yield return "sendtochaterror The module is not yet ready to be interacted with. Please wait until the module activates or finishes giving a strike.";
            yield break;
        }
        if (Regex.IsMatch(command, @"^\s*(((?=[cb])(colo(u|\(u\))?r)?(blind)?)|(I'm\s+colo(u|\(u\))?rblind!)|(((Where\s+are)|(What))\s+colo(u|\(u\))?rs\?)|(Colo(u|\(u\))?r\s+god\s+please\s+help\s+me))\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) && !colorblind)
        {
            colorblind = true;
            for (int index = 0; index < 7; index++)
                colorblindTexts[index].gameObject.SetActive(true);
            yield return null;
            yield break;
        }
        string[] parameters = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (Regex.IsMatch(parameters[0], @"^\s*cut\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) && parameters.Length == 2)
        {
            if (Regex.IsMatch(parameters[1], @"^\s*[1-7]\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {   
                int wireNumber;
                if (int.TryParse(parameters[1], out wireNumber))
                {
                    if (!isCut[wireNumber - 1])
                    {
                        yield return null;
                        wires[wireNumber - 1].OnInteract();
                        yield return new WaitForSeconds(0.1f);
                    }
                    else
                    {
                        yield return "sendtochaterror This wire is already cut. Please only cut the wire that haven't been cut yet.";
                        yield break;
                    }

                }
                else
                {
                    yield return "sendtochaterror ERROR: Couldn't parse the command correctly. Please report this to the current maintainer as it may indicate a bug.";
                    yield break;
                }
            }
            else
            {
                yield return "sendtochaterror Invalid command: Could not find the wire to cut. Wires are numbered 1 - 7 from top to bottom.";
                yield break;
            }
        }
        else
        {
            yield return "sendtochaterror Invalid command: The command must start with \"cut\" and followed by a number from 1 - 7 with space in between, or the command must match with any of the colo(u)rblind commands.";
            yield break;
        }
        yield break;
    }
}
