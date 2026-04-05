using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// Создаёт тестовую платформу для проверки персонажа
    /// Удаляй этот скрипт когда будет готов мир
    /// </summary>
    public class TestPlatformCreator : MonoBehaviour
    {
        [Header("Настройки платформы")]
        [Tooltip("Размер платформы")]
        [SerializeField] private Vector3 platformSize = new Vector3(50, 1, 50);

        [Tooltip("Позиция платформы")]
        [SerializeField] private Vector3 platformPosition = new Vector3(0, 0, 0);

        [Tooltip("Цвет платформы")]
        [SerializeField] private Color platformColor = Color.gray;

        [Header("Стена-ориентир")]
        [Tooltip("Создать стену для ориентации")]
        [SerializeField] private bool createWall = true;

        private void Start()
        {
            CreatePlatform();
            if (createWall) CreateWall();
        }

        private void CreatePlatform()
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "TestPlatform";
            platform.transform.position = platformPosition;
            platform.transform.localScale = platformSize;

            // Материал с фоллбэком
            Material mat = null;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader != null)
            {
                mat = new Material(shader);
                mat.color = platformColor;
                platform.GetComponent<Renderer>().material = mat;
            }
        }

        private void CreateWall()
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TestWall";
            wall.transform.position = platformPosition + new Vector3(0, 3, -platformSize.z / 2 - 1);
            wall.transform.localScale = new Vector3(platformSize.x, 6, 1);

            Material mat = null;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader != null)
            {
                mat = new Material(shader);
                mat.color = Color.blue;
                wall.GetComponent<Renderer>().material = mat;
            }
        }
    }
}
