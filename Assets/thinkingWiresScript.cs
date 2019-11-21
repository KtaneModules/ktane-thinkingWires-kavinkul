using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;

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
    private List<string> boxColor = new List<string>();
    private int firstWireToCut;
    private bool secondStage;
    private string secondWireToCut;
    private string screenNumber;
    private bool handlingStrike;
    private bool[] isCut = new bool[7];
    private bool colorblind = false;
   
    bool wireToCutFound = false; //A measure to prevent hanging the game from infinite while loop
    bool breakSuccessful = false;
    bool doorOpened = false;
    bool activated = false;
    //Logging
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

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
        if (doorOpened)
        {
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
        }
        if (!moduleSolved && !handlingStrike && doorOpened)
        {
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
        for (int index = 0; index < 7; index++)
        {
            randomColorIndex = Rnd.Range(0, 8);
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
        while (!wireToCutFound && boxCounter < 15)
        {
            switch (currentBoxIndex)
            {
                case 0:
                    if (wireColorNames.Count(x => x == "Red" || x == "Green" || x == "Blue") >= 3)
                    {
                        currentBoxIndex = 3;
                        Debug.LogFormat("[Thinking Wires #{0}] Number of primary wires: {1}", moduleId, "3+");
                    }
                    else
                    {
                        currentBoxIndex = 1;
                        Debug.LogFormat("[Thinking Wires #{0}] Number of primary wires: {1}", moduleId, "0 - 2");
                    }
                    boxCounter++;
                    boxColor.Add("Red");
                    break;
                case 1:
                    if (wireColorNames[4] == "White" || wireColorNames[4] == "Black")
                    {
                        currentBoxIndex = 12;
                        Debug.LogFormat("[Thinking Wires #{0}] 5th Wire white or black: {1}", moduleId, "Yes");
                    }
                    else
                    {
                        currentBoxIndex = 2;
                        Debug.LogFormat("[Thinking Wires #{0}] 5th Wire white or black: {1}", moduleId, "No");
                    }

                    boxCounter++;
                    boxColor.Add("Blue");
                    break;
                case 2:
                    breakSuccessful = false;
                    for (int index = 0; index < wireColorNames.Length - 1; index++)
                    {
                        string color1 = wireColorNames[index];
                        string color2 = wireColorNames[index + 1];
                        if ((color1 == "Red" && color2 == "Cyan") || (color1 == "Cyan" && color2 == "Red") || (color1 == "Green" && color2 == "Magenta") ||
                        (color1 == "Magenta" && color2 == "Green") || (color1 == "Blue" && color2 == "Yellow") || (color1 == "Yellow" && color2 == "Blue") ||
                        (color1 == "White" && color2 == "Black") || (color1 == "Black" && color2 == "White"))
                        {
                            currentBoxIndex = 11;
                            Debug.LogFormat("[Thinking Wires #{0}] 2 adjacent wires are complementary: {1}", moduleId, "True");
                            breakSuccessful = true;
                            break;
                        }
                    }
                    if (!breakSuccessful)
                    {
                        currentBoxIndex = 4;
                        Debug.LogFormat("[Thinking Wires #{0}] 2 adjacent wires are complementary: {1}", moduleId, "False");
                    }
                    boxCounter++;
                    boxColor.Add("Green");
                    break;
                case 3:
                    if (wireColorNames[6] == "Cyan" || wireColorNames[6] == "Magenta" || wireColorNames[6] == "Yellow")
                    {
                        currentBoxIndex = 2;
                        Debug.LogFormat("[Thinking Wires #{0}] 7th wire is secondary: {1}", moduleId, "True");
                    }
                    else
                    {
                        currentBoxIndex = 4;
                        Debug.LogFormat("[Thinking Wires #{0}] 7th wire is secondary: {1}", moduleId, "False");
                    }
                    boxCounter++;
                    boxColor.Add("Yellow");
                    break;
                case 4:
                    if (!wireColorNames.Any(color => color == "Blue"))
                    {
                        currentBoxIndex = 8;
                        Debug.LogFormat("[Thinking Wires #{0}] No wires are blue: {1}", moduleId, "True");
                    }
                    else
                    {
                        currentBoxIndex = 5;
                        Debug.LogFormat("[Thinking Wires #{0}] No wires are blue: {1}", moduleId, "False");
                    }
                    boxCounter++;
                    boxColor.Add("Cyan");
                    break;
                case 5:
                    if (wireColorNames.Distinct().Count() <= 5)
                    {
                        currentBoxIndex = 7;
                        Debug.LogFormat("[Thinking Wires #{0}] 5 or less wire colors present: {1}", moduleId, "Yes");
                    }
                    else
                    {
                        currentBoxIndex = 6;
                        Debug.LogFormat("[Thinking Wires #{0}] 5 or less wire colors present: {1}", moduleId, "No");
                    }
                    boxCounter++;
                    boxColor.Add("Blue");
                    break;
                case 6:
                    if (wireColorNames.Any(color => color == "Blue"))
                    {
                        currentBoxIndex = 7;
                        Debug.LogFormat("[Thinking Wires #{0}] Blue wire present: {1}", moduleId, "Yes");
                    }
                    else
                    {
                        currentBoxIndex = 10;
                        Debug.LogFormat("[Thinking Wires #{0}] Blue wire present: {1}", moduleId, "No");
                    }
                    boxCounter++;
                    boxColor.Add("Green");
                    break;
                case 7:
                    if (!boxColor.Contains(wireColorNames[5]))
                    {
                        currentBoxIndex = 9;
                        Debug.LogFormat("[Thinking Wires #{0}] One of the previous box color not the same as the 6th wire color: {1}", moduleId, "Yes");
                    }
                    else
                    {
                        currentBoxIndex = 26;
                        Debug.LogFormat("[Thinking Wires #{0}] One of the previous box color not the same as the 6th wire color: {1}", moduleId, "No");
                    }

                    boxCounter++;
                    boxColor.Add("Magenta");
                    break;
                case 8:
                    if (wireColorNames[2] != "Black" || wireColorNames[2] != "Blue" || wireColorNames[2] != "Yellow")
                    {
                        currentBoxIndex = 6;
                        Debug.LogFormat("[Thinking Wires #{0}] 3rd wire not black, blue, or yellow: {1}", moduleId, "Yes");
                    }
                    else
                    {
                        currentBoxIndex = 10;
                        Debug.LogFormat("[Thinking Wires #{0}] 3rd wire not black, blue, or yellow: {1}", moduleId, "No");
                    }
                    boxCounter++;
                    boxColor.Add("Red");
                    break;
                case 9:
                    if (!wireColorNames.Any(color => color == "White" || color == "Black"))
                    {
                        currentBoxIndex = 24;
                        Debug.LogFormat("[Thinking Wires #{0}] No wires are white or black: {1}", moduleId, "True");
                    }
                    else
                    {
                        currentBoxIndex = 25;
                        Debug.LogFormat("[Thinking Wires #{0}] No wires are white or black: {1}", moduleId, "False");
                    }
                    boxCounter++;
                    boxColor.Add("White");
                    break;
                case 10:
                    if (wireColorNames.Where((color, index) => index < 4).Distinct().Count() == 4)
                    {
                        currentBoxIndex = 9;
                        Debug.LogFormat("[Thinking Wires #{0}] First 4 wires all different colors: {1}", moduleId, "True");
                    }
                    else
                    {
                        currentBoxIndex = 15;
                        Debug.LogFormat("[Thinking Wires #{0}] First 4 wires all different colors: {1}", moduleId, "False");
                    }
                    boxCounter++;
                    boxColor.Add("Yellow");
                    break;
                case 11:
                    if (wireColorNames[1] == "White" || wireColorNames[1] == "Black" || wireColorNames[1] == "Red")
                    {
                        currentBoxIndex = 10;
                        Debug.LogFormat("[Thinking Wires #{0}] 2nd wire white black or red: {1}", moduleId, "Yes");
                    }
                    else
                    {
                        currentBoxIndex = 14;
                        Debug.LogFormat("[Thinking Wires #{0}] 2nd wire white black or red: {1}", moduleId, "No");
                    }
                    boxCounter++;
                    boxColor.Add("Black");
                    break;
                case 12:
                    if (!wireColorNames.Contains("Yellow"))
                    {
                        currentBoxIndex = 13;
                        Debug.LogFormat("[Thinking Wires #{0}] No wires are yellow: {1}", moduleId, "True");
                    }
                    else
                    {
                        currentBoxIndex = 11;
                        Debug.LogFormat("[Thinking Wires #{0}] No wires are yellow: {1}", moduleId, "False");
                    }
                    boxCounter++;
                    boxColor.Add("Green");
                    break;
                case 13:
                    if (wireColorNames[0] == "Blue" || wireColorNames[0] == "Cyan" || wireColorNames[0] == "Green")
                    {
                        currentBoxIndex = 14;
                        Debug.LogFormat("[Thinking Wires #{0}] 1st wire blue, cyan, or green: {1}", moduleId, "Yes");
                    }
                    else
                    {
                        currentBoxIndex = 16;
                        Debug.LogFormat("[Thinking Wires #{0}] 1st wire blue, cyan, or green: {1}", moduleId, "No");
                    }
                    boxCounter++;
                    boxColor.Add("Cyan");
                    break;
                case 14:
                    currentBoxIndex = 17;
                    Debug.LogFormat("[Thinking Wires #{0}] 2 adjacent wires are the same color (Dummy Rule)", moduleId);
                    boxCounter++;
                    boxColor.Add("White");
                    break;
                case 15:
                    int primaryCount = wireColorNames.Count(x => x == "Red" || x == "Green" || x == "Blue");
                    int secondaryCount = wireColorNames.Count(x => x == "Cyan" || x == "Magenta" || x == "Yellow");
                    int bwCount = wireColorNames.Count(x => x == "White" || x == "Black");
                    if (primaryCount > secondaryCount && primaryCount > bwCount)
                    {
                        currentBoxIndex = 19;
                        Debug.LogFormat("[Thinking Wires #{0}] Most common wire colors: {1}", moduleId, "RGB");
                    }
                    else if (secondaryCount > primaryCount && secondaryCount > bwCount)
                    {
                        currentBoxIndex = 24;
                        Debug.LogFormat("[Thinking Wires #{0}] Most common wire colors: {1}", moduleId, "CMY");
                    }
                    else if (bwCount > primaryCount && bwCount > secondaryCount)
                    {
                        currentBoxIndex = 23;
                        Debug.LogFormat("[Thinking Wires #{0}] Most common wire colors: {1}", moduleId, "WK");
                    }
                    else
                    {
                        currentBoxIndex = 14;
                        Debug.LogFormat("[Thinking Wires #{0}] Most common wire colors: {1}", moduleId, "Tie");
                    }
                    boxCounter++;
                    boxColor.Add("Black");
                    break;
                case 16:
                    if (boxColor.Contains(wireColorNames[3]))
                    {
                        currentBoxIndex = 18;
                        Debug.LogFormat("[Thinking Wires #{0}] One of the previous box color the same as the 4th wire color: {1}", moduleId, "Yes");
                    }
                    else
                    {
                        currentBoxIndex = 17;
                        Debug.LogFormat("[Thinking Wires #{0}] One of the previous box color the same as the 4th wire color: {1}", moduleId, "No");
                    }
                    boxCounter++;
                    boxColor.Add("Magenta");
                    break;
                case 17:
                    breakSuccessful = false;
                    for (int index = 1; index < wireColorNames.Length - 1; index++)
                    {
                        string color1 = wireColorNames[index - 1];
                        string color2 = wireColorNames[index];
                        string color3 = wireColorNames[index + 1];
                        if ((color2 == "Cyan" && ((color1 == "Green" && color3 == "Blue") || (color1 == "Blue" && color3 == "Green"))) ||
                        (color2 == "Magenta" && ((color1 == "Red" && color3 == "Blue") || (color1 == "Blue" && color3 == "Red"))) ||
                        (color2 == "Yellow" && ((color1 == "Red" && color3 == "Green") || (color1 == "Green" && color3 == "Red"))))
                        {
                            currentBoxIndex = 19;
                            Debug.LogFormat("[Thinking Wires #{0}] Secondary color adjacent to both it's primary colors: {1}", moduleId, "True");
                            breakSuccessful = true;
                            break;
                        }
                    }
                    if (!breakSuccessful)
                    {
                        currentBoxIndex = 21;
                        Debug.LogFormat("[Thinking Wires #{0}] Secondary color adjacent to both it's primary colors: {1}", moduleId, "False");
                    }
                    boxCounter++;
                    boxColor.Add("Blue");
                    break;
                case 18:
                    if (wireColorNames.Any(color => wireColorNames.Where(x => x == color).Count() == 3))
                    {
                        currentBoxIndex = 21;
                        Debug.LogFormat("[Thinking Wires #{0}] 3 wires are the same color: {1}", moduleId, "True");
                    }
                    else
                    {
                        currentBoxIndex = 20;
                        Debug.LogFormat("[Thinking Wires #{0}] 3 wires are the same color: {1}", moduleId, "False");
                    }
                    boxCounter++;
                    boxColor.Add("Yellow");
                    break;
                case 19:
                    currentBoxIndex = 22;
                    Debug.LogFormat("[Thinking Wires #{0}] No condition to check. (An empty box)", moduleId);
                    boxCounter++;
                    boxColor.Add("Cyan");
                    break;
                case 20:
                    firstWireToCut = 1;
                    wireToCutFound = true;
                    break;
                case 21:
                    firstWireToCut = 2;
                    wireToCutFound = true;
                    break;
                case 22:
                    firstWireToCut = 3;
                    wireToCutFound = true;
                    break;
                case 23:
                    firstWireToCut = 4;
                    wireToCutFound = true;
                    break;
                case 24:
                    firstWireToCut = 5;
                    wireToCutFound = true;
                    break;
                case 25:
                    firstWireToCut = 6;
                    wireToCutFound = true;
                    break;
                case 26:
                    firstWireToCut = 7;
                    wireToCutFound = true;
                    break;
            }
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
        while(!moduleSolved && (handlingStrike || !doorOpened))
        {
            yield return new WaitForSeconds(0.1f);
        }
        yield return null;

        if (!secondStage)
        {
            wires[firstWireToCut - 1].OnInteract();
            yield return new WaitForSeconds(0.2f);
        }
        for (int index = 0; index < 7; index++)
        {
            if(!isCut[index] && originalColorNames[index] == secondWireToCut)
            {
                wires[index].OnInteract();
                yield return new WaitForSeconds(0.2f);
                break;
            }
        }
        yield break;
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
        string[] parameters = command.Split(' ');
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
