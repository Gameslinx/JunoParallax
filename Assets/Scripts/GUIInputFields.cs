using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

public class InputFields
{
    private static int activeFieldID = -1;

    private static float activeFloatFieldLastValue = 0;
    private static string activeFloatFieldString = "";

    private static Color activeColorFieldLastValue = new Color();
    private static string activeColorFieldString = "";

    private static string activeTexFieldLastValue = "";
    private static string activeTexFieldString = "";

    private static float activeSliderFieldLastValue = 0;
    private static string activeSliderFieldString = "";

    private static string activeVectorXString = "";
    private static string activeVectorYString = "";
    private static string activeVectorZString = "";
    private static float activeVectorXStringLV = 0;
    private static float activeVectorYStringLV = 0;
    private static float activeVectorZStringLV = 0;

    private static readonly GUIStyle TexStyle = new GUIStyle(GUI.skin.textArea) { wordWrap = true };
    private static readonly GUIStyle ResetButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 9 };
    private static readonly GUIStyle FloatStyle = new GUIStyle(GUI.skin.textArea);
    private static readonly GUIStyle ColorStyle = new GUIStyle(GUI.skin.textArea);

    /// <summary>
    /// Float input field for in-game cfg editing. Behaves exactly like UnityEditor.EditorGUILayout.FloatField
    /// </summary>
    public static float FloatField(float value)
    {
        // Get rect and control for this float field for identification
        Rect pos = GUILayoutUtility.GetRect(new GUIContent(value.ToString(CultureInfo.InvariantCulture)),
            GUI.skin.label, GUILayout.ExpandWidth(false), GUILayout.MinWidth(50));

        int floatFieldID = GUIUtility.GetControlID("FloatField".GetHashCode(), FocusType.Keyboard, pos) + 1;
        if (floatFieldID == 0)
            return value;

        // has the value been recorded?
        bool recorded = activeFieldID == floatFieldID;
        // is the field being edited?
        bool active = floatFieldID == GUIUtility.keyboardControl;

        if (active && recorded && activeFloatFieldLastValue != value)
        {
            // Value has been modified externally
            activeFloatFieldLastValue = value;
            activeFloatFieldString = value.ToString(CultureInfo.InvariantCulture);
        }

        // Get stored string for the text field if this one is recorded
        string str = recorded ? activeFloatFieldString : value.ToString(CultureInfo.InvariantCulture);

        // pass it in the text field
        string strValue = GUI.TextField(pos, str, FloatStyle);

        // Update stored value if this one is recorded
        if (recorded)
            activeFloatFieldString = strValue;

        // Try Parse if value got changed. If the string could not be parsed, ignore it and keep last value
        bool parsed = true;
        if (strValue != value.ToString(CultureInfo.InvariantCulture))
        {
            parsed = float.TryParse(strValue, out float newValue);
            if (parsed)
                value = activeFloatFieldLastValue = newValue;
        }

        if (active && !recorded)
        {
            // Gained focus this frame
            activeFieldID = floatFieldID;
            activeFloatFieldString = strValue;
            activeFloatFieldLastValue = value;
        }
        else if (!active && recorded)
        {
            // Lost focus this frame
            activeFieldID = -1;
            if (!parsed)
                value = strValue.ForceParseFloat();
        }

        return value;
    }
    public static Vector3 VectorField(Vector3 value)
    {
        // Get rect and control for this float field for identification
        Rect pos = GUILayoutUtility.GetRect(new GUIContent(value.x.ToString(CultureInfo.InvariantCulture)),
            GUI.skin.label, GUILayout.ExpandWidth(false), GUILayout.MinWidth(50));
        pos.x -= 110;
        Rect pos2 = pos;
        pos2.x += 55;
        Rect pos3 = pos2;
        pos3.x += 55;


        string str = value.x.ToString(CultureInfo.InvariantCulture);
        string str2 = value.y.ToString(CultureInfo.InvariantCulture);
        string str3 = value.z.ToString(CultureInfo.InvariantCulture);

        // pass it in the text field
        string strValue = GUI.TextField(pos, str, FloatStyle);
        string strValue2 = GUI.TextField(pos2, str2, FloatStyle);
        string strValue3 = GUI.TextField(pos3, str3, FloatStyle);
        // Update stored value if this one is recorded


        // Try Parse if value got changed. If the string could not be parsed, ignore it and keep last value
        bool parsed = true;
        if (strValue + ", " + strValue2 + ", " + strValue3 != value.x.ToString(CultureInfo.InvariantCulture))
        {
            parsed = float.TryParse(strValue, out float newValue);
            if (parsed)
                value.x = activeVectorXStringLV = newValue;
            parsed = float.TryParse(strValue2, out float newValue2);
            if (parsed)
                value.y = activeVectorYStringLV = newValue2;
            parsed = float.TryParse(strValue3, out float newValue3);
            if (parsed)
                value.z = activeVectorZStringLV = newValue3;
        }

        activeFieldID = -1;

        return value;
    }
    /// <summary>
    /// Color input field for in-game cfg editing. Parses basic colors (e.g. "yellow"), as well as any
    /// string in the format "r, g, b, (a)". Ignores any character before or after the comma-separated floats.
    /// </summary>
    public static Color ColorField(Color value)
    {
        // Get rect and control for this float field for identification
        Rect pos = GUILayoutUtility.GetRect(new GUIContent(value.ToString()), GUI.skin.label,
            GUILayout.ExpandWidth(false), GUILayout.MinWidth(220));
        int colorFieldID = GUIUtility.GetControlID("ColorField".GetHashCode(), FocusType.Keyboard, pos) + 1;
        if (colorFieldID == 0)
            return value;

        // has the value been recorded?
        bool recorded = activeFieldID == colorFieldID;
        // is the field being edited?
        bool active = colorFieldID == GUIUtility.keyboardControl;

        if (active && recorded && activeColorFieldLastValue != value)
        {
            // Value has been modified externally
            activeColorFieldLastValue = value;
            activeColorFieldString = value.ToString();
        }

        // Get stored string for the text field if this one is recorded
        string str = recorded ? activeColorFieldString : value.ToString();

        // pass it in the text field
        string strValue = GUI.TextField(pos, str, ColorStyle);

        // Update stored value if this one is recorded
        if (recorded)
            activeColorFieldString = strValue;

        // Try Parse if value got changed. If the string could not be parsed, ignore it and keep last value
        bool parsed = true;
        if (strValue != value.ToString())
        {
            parsed = ColorUtility.TryParseHtmlString(strValue, out Color newValue);
            if (parsed)
                value = activeColorFieldLastValue = newValue;
        }

        if (active && !recorded)
        {
            // Gained focus this frame
            activeFieldID = colorFieldID;
            activeColorFieldString = strValue;
            activeColorFieldLastValue = value;
        }
        else if (!active && recorded)
        {
            // Lost focus this frame
            activeFieldID = -1;
            if (!parsed)
                value = strValue.ForceParseColor(activeColorFieldLastValue);
        }

        return value;
    }
    public static float SliderField(float value, float min, float max)
    {
        // Get rect and control for this float field for identification
        Rect pos = GUILayoutUtility.GetRect(new GUIContent(value.ToString()), GUI.skin.label,
            GUILayout.ExpandWidth(false), GUILayout.MinWidth(220));
        int colorFieldID = GUIUtility.GetControlID("SliderField".GetHashCode(), FocusType.Keyboard, pos) + 1;
        if (colorFieldID == 0)
            return value;

        // has the value been recorded?
        bool recorded = activeFieldID == colorFieldID;
        // is the field being edited?
        bool active = colorFieldID == GUIUtility.keyboardControl;

        if (active && recorded && activeSliderFieldLastValue != value)
        {
            // Value has been modified externally
            activeSliderFieldLastValue = value;
            activeSliderFieldString = value.ToString();
        }

        // Get stored string for the text field if this one is recorded
        string str = recorded ? activeSliderFieldString : value.ToString();

        // pass it in the text field
        string strValue = GUI.HorizontalSlider(pos, value, min, max).ToString("F4");

        // Update stored value if this one is recorded
        if (recorded)
            activeSliderFieldString = strValue;

        // Try Parse if value got changed. If the string could not be parsed, ignore it and keep last value
        bool parsed = true;
        if (strValue != value.ToString())
        {
            parsed = float.TryParse(strValue, out float newValue);
            if (parsed)
                value = activeSliderFieldLastValue = newValue;
        }

        if (active && !recorded)
        {
            // Gained focus this frame
            activeFieldID = colorFieldID;
            activeSliderFieldString = strValue;
            activeSliderFieldLastValue = value;
        }
        else if (!active && recorded)
        {
            // Lost focus this frame
            activeFieldID = -1;
            if (!parsed)
                value = strValue.ForceParseFloat();
        }

        return value;
    }
    /// <summary>
    /// Special-purpose Text input field for in-game editing. Meant for dds or png textures' paths.
    /// Check if texture file exists before applying the value.
    /// </summary>
    public static string TexField(string value)
    {
        // Get rect and control for this float field for identification
        Rect pos = GUILayoutUtility.GetRect(new GUIContent(value.ToString(CultureInfo.InvariantCulture)),
            GUI.skin.label, GUILayout.ExpandWidth(false), GUILayout.MinWidth(200));

        int floatFieldID = GUIUtility.GetControlID("StringField".GetHashCode(), FocusType.Keyboard, pos) + 1;
        if (floatFieldID == 0)
            return value;

        // has the value been recorded?
        bool recorded = activeFieldID == floatFieldID;
        // is the field being edited?
        bool active = floatFieldID == GUIUtility.keyboardControl;

        if (active && recorded && activeTexFieldLastValue != value)
        {
            // Value has been modified externally
            activeTexFieldLastValue = value;
            activeTexFieldString = value.ToString(CultureInfo.InvariantCulture);
        }

        // Get stored string for the text field if this one is recorded
        string str = recorded ? activeTexFieldString : value.ToString(CultureInfo.InvariantCulture);

        // pass it in the text field
        string strValue = GUI.TextField(pos, str, FloatStyle);

        // Update stored value if this one is recorded
        if (recorded)
            activeTexFieldString = strValue;

        // Try Parse if value got changed. If the string could not be parsed, ignore it and keep last value
        bool parsed = true;
        if (strValue != value.ToString(CultureInfo.InvariantCulture))
        {

            value = activeTexFieldLastValue = strValue;
        }

        if (active && !recorded)
        {
            // Gained focus this frame
            activeFieldID = floatFieldID;
            activeTexFieldString = strValue;
            activeTexFieldLastValue = value;
        }
        else if (!active && recorded)
        {
            // Lost focus this frame
            activeFieldID = -1;
            if (!parsed)
                value = strValue;
        }

        return value;
    }

    /// <summary>
    /// Float input field with label. Displays the label on the left, and the input field on the right.
    /// </summary>
    public static float FloatField(string label, float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label + " [float] ", GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();

        value = FloatField(value);


        GUILayout.EndHorizontal();
        return value;
    }
    public static Vector3 VectorField(string label, Vector3 value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label + " [float] ", GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();

        value = VectorField(value);


        GUILayout.EndHorizontal();
        return new Vector3(value.x, value.y, value.z);
    }
    /// <summary>
    /// Int input field with label. Displays the label on the left, and the input field on the right.
    /// Just a FloatField, but with returned value being rounded to nearest int.
    /// </summary>
    public static int IntField(string label, float value, int minValue, int maxValue)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label + " [float] ", GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();

        value = FloatField(value);

        GUILayout.EndHorizontal();
        return Mathf.Clamp(Mathf.RoundToInt(value), minValue, maxValue);
    }

    /// <summary>
    /// Color input field with label. Displays the label on the left, and the input field on the right.
    /// </summary>
    public static Color ColorField(string label, Color value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label + " [Color] ", GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();

        value = ColorField(value);


        GUILayout.EndHorizontal();
        return value;
    }
    public static float SliderField(string label, float value, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label + " [" + value.ToString() + "] ", GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();

        value = SliderField(value, min, max);


        GUILayout.EndHorizontal();
        return value;
    }
    /// <summary>
    /// Texture path input field with label. Displays the label on the left, and the input field on the right.
    /// </summary>
    public static string TexField(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label + " [Texture] ", GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();

        value = TexField(value);


        GUILayout.EndHorizontal();
        return value;
    }

}
public static class Parsers
{
    private const string ColorStringPattern = @"(\d\.\d*)[^\d]*(\d\.\d*)[^\d]*(\d\.\d*)[^\d]*(\d\.\d*)?";

    /// <summary>
    /// Forces to parse to float by cleaning string if necessary
    /// </summary>
    public static float ForceParseFloat(this string str)
    {
        // try parse
        if (float.TryParse(str, out float value))
            return value;

        // Clean string if it could not be parsed
        bool recordedDecimalPoint = false;
        var charList = new List<char>(str);
        for (int cnt = 0; cnt < charList.Count; cnt++)
        {
            UnicodeCategory type = CharUnicodeInfo.GetUnicodeCategory(str[cnt]);
            if (type != UnicodeCategory.DecimalDigitNumber)
            {
                charList.RemoveRange(cnt, charList.Count - cnt);
                break;
            }

            if (str[cnt] != '.') continue;

            if (recordedDecimalPoint)
            {
                charList.RemoveRange(cnt, charList.Count - cnt);
                break;
            }

            recordedDecimalPoint = true;
        }

        // Parse again
        if (charList.Count == 0)
            return 0;
        str = new string(charList.ToArray());
        if (!float.TryParse(str, out value))
            Debug.LogError("Could not parse " + str);

        return value;
    }

    public static Color ForceParseColor(this string str, Color defaultValue)
    {
        Color newValue;

        if (ColorUtility.TryParseHtmlString(str, out newValue))
            return newValue;

        Regex r = new Regex(ColorStringPattern);
        Match m = r.Match(str);

        if (!m.Success)
            return defaultValue;


        float[] col = { 1f, 1f, 1f, 1f };
        for (int i = 0; i < m.Groups.Count - 1; i++)
        {
            // groups[0] is the entire match, groups[1] is the first group 
            col[i] = Mathf.Clamp01(m.Groups[i + 1].ToString().ForceParseFloat());
        }

        return new Color(col[0], col[1], col[2], col[3]);
    }
}