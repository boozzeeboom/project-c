// Cloudy Navigator — корень окна. Шаг 2: пустое окно; шаг 3: окно владеет читающим слоем CnDataFeed.
// Дерево появится на шаге 4 по USS-контракту мода.
//
// Класс не знает ни о UIDocument, ни о панели: хост передаёт родителя в Mount и тикает Update(dt).
// Такой же приём, как у ProjectCMainMenuWindow — окно монтируется на любой VisualElement и не владеет панелью.
//
// Всё собственное время окна считается в Update(dt) от хоста: setTimeout/@keyframes в порте нет (04 §1).
using ProjectCCloudyNavigator.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator
{
    /// <summary>Дерево и поведение окна. Пока шаг 3 — корень, контракт жизненного цикла и данные.</summary>
    public sealed class CloudyNavigatorWindow
    {
        private VisualElement _root;
        private readonly CnDataFeed _feed;

        /// <summary>Окно читает игру, но не пишет в неё: единственный владелец данных — CnDataFeed.</summary>
        public CnDataFeed Data => _feed;

        /// <summary>log — писать ли [CN]-снимок в консоль для сверки с легаси (только диагностика).</summary>
        public CloudyNavigatorWindow(bool log)
        {
            _feed = new CnDataFeed(log);
        }

        /// <summary>Корень окна. null до Mount.</summary>
        public VisualElement Root => _root;

        public void Mount(VisualElement parent)
        {
            if (parent == null) return;
            if (_root != null) return;

            _root = new VisualElement { name = "cn-root" };
            _root.AddToClassList("cn-root");
            parent.Add(_root);
        }

        /// <summary>Кадр окна. dt уже зажат хостом (≤ 0.1 с), чтобы паузы редактора не рвали анимации.</summary>
        public void Update(float dt)
        {
            // шаг 4+: поле дыма, тексты, попапы, шкала
            _feed.Tick(dt);
        }

        public void Dispose()
        {
            _root?.RemoveFromHierarchy();
            _root = null;
        }
    }
}
