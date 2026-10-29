// Cloudy Navigator — заметки графа связей (мод v4-2: src/rel-graph.js, снапшот localStorage 'v4.relations.layout').
//
// Заметка графа — личная запись игрока: в игре нет субъекта «заметка», поэтому порт держит их сам, как мод держал их
// в снапшоте раскладки. Файл лежит в Application.persistentDataPath, то есть вне Assets и вне Assets/_Project:
// удаление слоя порта возвращает игру в исходное состояние и следов в её данных не оставляет.
//
// Поля — те же, что мод кладёт в снапшот (rel-graph.js:635-640): id, x, y, text, attach, collapsed, rot. Смещение
// заметки относительно узла (off в моде) не хранится: узлы порта не перетаскиваются, раскладка детерминированная,
// и единственное, что двигает узел, — зум. Трос заметки рисуется от заметки к текущему центру узла, поэтому связь
// остаётся правдивой и без запомненного смещения.
//
// При чтении каждая запись проверяется: пустой текст становится «Заметка», координаты зажимаются в допустимые,
// неизвестный ключ привязки остаётся как есть (узла может не быть в текущем снапшоте — тогда троса просто нет).
// Ошибки диска не пробрасываются наружу: заметки — украшение, а не игровое состояние.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ProjectCCloudyNavigator.Data
{
    /// <summary>Одна заметка графа: место на полотне, текст, привязка к субъекту, свёрнутость и наклон.</summary>
    [Serializable]
    public sealed class CnRelNote
    {
        public string id;
        public float x;         // координаты полотна (в тех же пикселях, что и узлы)
        public float y;
        public string text;
        public string attach;   // ключ узла графа («f:1» / «n:mira_01»); пусто — не прикреплена
        public bool collapsed;
        public float rot;       // наклон стикера в градусах (rel-graph.js:1364 — 1.5 + шаг)
    }

    /// <summary>Обёртка файла: JsonUtility не умеет писать массив верхнего уровня.</summary>
    [Serializable]
    internal sealed class CnRelNoteFile
    {
        public CnRelNote[] items;
    }

    /// <summary>Хранилище заметок графа связей; пишет только в Application.persistentDataPath.</summary>
    public sealed class CnRelNotesStore
    {
        /// <summary>Имя файла заметок графа в Application.persistentDataPath.</summary>
        public const string DefaultFileName = "cn-rel-notes.json";

        /// <summary>Текст заметки по умолчанию — как в моде (`text: 'Заметка'`, rel-graph.js:1365).</summary>
        public const string DefaultText = "Заметка";

        /// <summary>Потолок координат: заметка не может уехать в бесконечность испорченным файлом.</summary>
        private const float MaxCoord = 20000f;

        private readonly List<CnRelNote> _notes = new List<CnRelNote>(8);
        private readonly string _file;
        private bool _loaded;
        private bool _reported;
        private int _seq;

        /// <summary>file — путь для проверок; без него файл лежит в Application.persistentDataPath.</summary>
        public CnRelNotesStore(string file = null)
        {
            _file = string.IsNullOrEmpty(file)
                ? Path.Combine(Application.persistentDataPath, DefaultFileName)
                : file;
        }

        public string FilePath => _file;

        /// <summary>Последняя ошибка диска (пусто — всё в порядке). Для диагностики, не для игрока.</summary>
        public string LastError { get; private set; } = string.Empty;

        public int Count
        {
            get
            {
                EnsureLoaded();
                return _notes.Count;
            }
        }

        /// <summary>Заметки в порядке добавления. Только чтение: менять их можно лишь методами хранилища.</summary>
        public IReadOnlyList<CnRelNote> Notes
        {
            get
            {
                EnsureLoaded();
                return _notes;
            }
        }

        public void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            Load();
        }

        public CnRelNote Find(string id)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < _notes.Count; i++)
                if (_notes[i].id == id) return _notes[i];
            return null;
        }

        /// <summary>Новая заметка в точке полотна. Идентификатор и наклон — как в моде (rel-graph.js:1363-1365).</summary>
        public CnRelNote Add(float x, float y)
        {
            EnsureLoaded();

            var note = new CnRelNote
            {
                id = NextId(),
                x = ClampCoord(x),
                y = ClampCoord(y),
                text = DefaultText,
                attach = null,
                collapsed = false,
                rot = 1.5f + (_seq % 3) * 0.5f
            };

            _notes.Add(note);
            Save();
            return note;
        }

        /// <summary>Текст заметки. Пустой текст не сохраняется — остаётся прежний (в моде textarea всегда с текстом).</summary>
        public bool SetText(string id, string text)
        {
            var note = Find(id);
            if (note == null) return false;

            note.text = Clean(text);
            Save();
            return true;
        }

        /// <summary>Место заметки на полотне (после перетаскивания).</summary>
        public bool SetPos(string id, float x, float y)
        {
            var note = Find(id);
            if (note == null) return false;

            note.x = ClampCoord(x);
            note.y = ClampCoord(y);
            Save();
            return true;
        }

        /// <summary>Привязка к субъекту графа; пустая строка снимает привязку.</summary>
        public bool Attach(string id, string key)
        {
            var note = Find(id);
            if (note == null) return false;

            note.attach = string.IsNullOrEmpty(key) ? null : key;
            Save();
            return true;
        }

        public bool SetCollapsed(string id, bool collapsed)
        {
            var note = Find(id);
            if (note == null) return false;
            if (note.collapsed == collapsed) return true;

            note.collapsed = collapsed;
            Save();
            return true;
        }

        public bool Delete(string id)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(id)) return false;

            for (int i = 0; i < _notes.Count; i++)
            {
                if (_notes[i].id != id) continue;
                _notes.RemoveAt(i);
                Save();
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ диск

        private void Load()
        {
            _notes.Clear();

            try
            {
                if (!File.Exists(_file)) return;

                string raw = File.ReadAllText(_file, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(raw)) return;

                var file = JsonUtility.FromJson<CnRelNoteFile>(raw);
                if (file == null || file.items == null) return;

                for (int i = 0; i < file.items.Length; i++)
                {
                    var note = file.items[i];
                    if (note == null) continue;

                    note.text = Clean(note.text);
                    note.x = ClampCoord(note.x);
                    note.y = ClampCoord(note.y);
                    note.attach = string.IsNullOrEmpty(note.attach) ? null : note.attach;
                    note.rot = Mathf.Clamp(float.IsNaN(note.rot) ? 0f : note.rot, -8f, 8f);
                    note.id = string.IsNullOrEmpty(note.id) ? NextId() : note.id;
                    _notes.Add(note);
                }
            }
            catch (Exception e)
            {
                LastError = e.GetType().Name + ": " + e.Message;
                Report();
                _notes.Clear();
            }
        }

        private void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(_file);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var file = new CnRelNoteFile { items = _notes.ToArray() };
                File.WriteAllText(_file, JsonUtility.ToJson(file, true), Encoding.UTF8);
                LastError = string.Empty;
            }
            catch (Exception e)
            {
                LastError = e.GetType().Name + ": " + e.Message;
                Report();
            }
        }

        /// <summary>Одно предупреждение за жизнь хранилища: диск может быть только для чтения, спамить незачем.</summary>
        private void Report()
        {
            if (_reported) return;
            _reported = true;
            Debug.LogWarning("[CN] заметки графа: " + LastError + " (" + _file + ")");
        }

        /// <summary>Идентификаторы мода — «nt1», «nt2» (rel-graph.js:1364): продолжаем счёт с уже загруженных.</summary>
        private string NextId()
        {
            string id;
            do
            {
                id = "nt" + (++_seq);
            } while (Find(id) != null);

            return id;
        }

        private static float ClampCoord(float value)
        {
            if (float.IsNaN(value)) return 0f;
            return Mathf.Clamp(value, 0f, MaxCoord);
        }

        private static string Clean(string text)
        {
            string trimmed = (text ?? string.Empty).Trim();
            return trimmed.Length == 0 ? DefaultText : trimmed;
        }
    }
}
