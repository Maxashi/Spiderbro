#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SpiderLeg))]
public class SpiderLegPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 10; // Adjust as needed
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Find the SerializedProperties by name
        var legTargetProp = property.FindPropertyRelative(nameof(SpiderLeg.legTarget));
        var desiredLegPositionProp = property.FindPropertyRelative(nameof(SpiderLeg.desiredLegPosition));
        var lastLegPositionProp = property.FindPropertyRelative(nameof(SpiderLeg.lastLegPosition));
        var defaultLegPositionProp = property.FindPropertyRelative(nameof(SpiderLeg.defaultLegPosition));
        var isMovingProp = property.FindPropertyRelative(nameof(SpiderLeg.isMoving));

        var defaultSize = position.size;

        // Customize how you want to display the properties
        // (e.g., use EditorGUI.PropertyField to create fields)

        position.size = new Vector2(position.size.x, 22);
        EditorGUI.PropertyField(position, legTargetProp);
        position.size = defaultSize;
        position.y += EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, desiredLegPositionProp);
        position.y += EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, lastLegPositionProp);
        position.y += EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, defaultLegPositionProp);
        position.y += EditorGUIUtility.singleLineHeight;

        position.size = new Vector2(position.size.x, 22);
        EditorGUI.PropertyField(position, isMovingProp);
        EditorGUI.EndProperty();
    }
}
#endif
