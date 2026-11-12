using UnityEngine;

namespace BombBrick.Game.Scenes
{
    public abstract class ProductionSceneController : MonoBehaviour
    {
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;

        protected GUIStyle TitleStyle => titleStyle ?? (titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        });

        protected GUIStyle BodyStyle => bodyStyle ?? (bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.82f, 0.86f, 0.92f) }
        });

        protected GUIStyle ButtonStyle => buttonStyle ?? (buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        });

        protected static Rect Centered(float width, float height, float yOffset = 0f)
        {
            return new Rect((BombBrickUiLayout.Width - width) * 0.5f, (BombBrickUiLayout.Height - height) * 0.5f + yOffset, width, height);
        }
    }
}
