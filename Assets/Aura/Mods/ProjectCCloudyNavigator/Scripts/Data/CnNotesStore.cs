// Cloudy Navigator — заметки календаря (мод v4-2: src/calendar.js, localStorage 'v4.calendar.notes').
//
// Заметка — личная запись игрока, а не игровые данные: в игре у дня нет источника заметок, поэтому порт держит их
// сам, ровно как мод держал их в localStorage. Файл лежит в Application.persistentDataPath, то есть вне Assets и
// вне Assets/_Project: удаление слоя порта возвращает игру в исходное состояние и следов в её данных не оставляет.
//
// Формат — JsonUtility, один массив. При чтении каждая запись проверяется (день ≥ 0, время 0…1439, тип из трёх
// известных, текст непустой), поэтому испорченный файл не ломает окно, а теряет только плохие строки.
// Ошибки диска не пробрасываются наружу: заметки — украшение, а не игровое состояние, и падать из-за них нельзя.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ProjectCCloudyNavigator.Data
{
    /// <summary>Одна заметка дня: день (абсолютный номер), минуты суток, тип и текст. Ключи — как в calendar.js.</summary>
    [Serializable]
    public sealed class CnNote
    {
        public string id;
        public int day;      // абсолютный номер дня: (год − 1) · днейВГоду + (месяц − 1) · днейВМесяце + (день − 1)
        public int time;     // игровые минуты суток 0…1439
        public string type;  // Remember / Business / Meeting
        public string text;
    }

    /// <summary>Обёртка файла: JsonUtility не умеет писать массив верхнего уровня.</summary>
    [Serializable]
    internal sealed class CnNoteFile
    {
        public CnNote[] items;
    }

    /// <summary>Хранилище заметок календаря и единственное место, где порт пишет на диск.</summary>
    public sealed class CnNotesStore
    {
        public const string Remember = "REMEMBER";
        public const string Business = "BUSINESS";
        public const string Meeting = "MEETING";

        /// <summary>Имя файла заметок в Application.persistentDataPath.</summary>
        public const string DefaultFileName = "cn-calendar-notes.json";

        private readonly List<CnNote> _notes = new List<CnNote>(16);
        private readonly string _file;
        private bool _loaded;
        private bool _reported;
        private int _seq;

        /// <summary>file — путь для проверок; без него файл лежит в Application.persistentDataPath.</summary>
        public CnNotesStore(string file = null)
        {
            _file = string.IsNullOrEmpty(file)
                ? Path.Combine(Application.persistentDataPath, DefaultFileName)
                : file;
        }

        public string FilePath => _file;

        /// <summary>Последняя ошибка диска (пусто — всё в порядке). Показывать её игроку незачем, она для диагностики.</summary>
        public string LastError { get; private set; } = string.Empty;

        public int Count
        {
            get
            {
                EnsureLoaded();
                return _notes.Count;
            }
        }

        /// <summary>Записи в порядке добавления. Только чтение: менять их можно лишь методами хранилища.</summary>
        public IReadOnlyList<CnNote> Notes
        {
            get
            {
                EnsureLoaded();
                return _notes;
            }
        }

        public static bool IsType(string key) => key == Remember || key == Business || key == Meeting;

        /// <summary>Подпись типа для строки события (calendar.js TYPE_LABEL).</summary>
        public static string TypeLabel(string key)
        {
            if (key == Business) return "дело";
            if (key == Meeting) return "встреча";
            return "помнить";
        }

        /// <summary>Подпись кнопки типа (calendar.js TYPES: ПОМНИТЬ / ДЕЛО / ВСТРЕЧА).</summary>
        public static string TypeCap(string key) => TypeLabel(key).ToUpperInvariant();

        public void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            Load();
        }

        /// <summary>Заметки выбранного дня в порядке файла.</summary>
        public void ForDay(int day, List<CnNote> into)
        {
            EnsureLoaded();
            if (into == null) return;

            into.Clear();
            for (int i = 0; i < _notes.Count; i++)
                if (_notes[i].day == day) into.Add(_notes[i]);
        }

        public int CountDay(int day)
        {
            EnsureLoaded();

            int count = 0;
            for (int i = 0; i < _notes.Count; i++)
                if (_notes[i].day == day) count++;
            return count;
        }

        public CnNote Find(string id)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < _notes.Count; i++)
                if (_notes[i].id == id) return _notes[i];
            return null;
        }

        /// <summary>Добавить заметку. День и время зажимаются в допустимые границы, пустой текст становится «Заметка».</summary>
        public CnNote Add(int day, int minutesOfDay, string type, string text)
        {
            EnsureLoaded();

            var note = new CnNote
            {
                id = NextId(),
                day = Mathf.Max(0, day),
                time = Mathf.Clamp(minutesOfDay, 0, 1439),
                type = IsType(type) ? type : Remember,
                text = Clean(text)
            };

            _notes.Add(note);
            Save();
            return note;
        }

        public bool Edit(CnNote note, int day, int minutesOfDay, string type, string text)
        {
            EnsureLoaded();
            if (note == null || !_notes.Contains(note)) return false;

            note.day = Mathf.Max(0, day);
            note.time = Mathf.Clamp(minutesOfDay, 0, 1439);
            note.type = IsType(type) ? type : Remember;
            note.text = Clean(text);
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

        /// <summary>«ОЧИСТИТЬ ДЕНЬ»: все заметки дня. Возвращает число удалённых.</summary>
        public int ClearDay(int day)
        {
            EnsureLoaded();

            int removed = _notes.RemoveAll(n => n.day == day);
            if (removed > 0) Save();
            return removed;
        }

        /// <summary>
        /// Подпись набора дней с заметками: по ней панель понимает, что точки в сетке устарели. Строка, а не
        /// перечисление, — сравнение с прошлым значением дешевле, чем обход сетки каждый кадр.
        /// </summary>
        public string DaySignature()
        {
            EnsureLoaded();
            if (_notes.Count == 0) return string.Empty;

            var days = new List<int>(_notes.Count);
            for (int i = 0; i < _notes.Count; i++)
                if (!days.Contains(_notes[i].day)) days.Add(_notes[i].day);
            days.Sort();

            var sb = new StringBuilder(days.Count * 5);
            for (int i = 0; i < days.Count; i++) sb.Append(days[i]).Append(',');
            return sb.ToString();
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

                var file = JsonUtility.FromJson<CnNoteFile>(raw);
                if (file == null || file.items == null) return;

                for (int i = 0; i < file.items.Length; i++)
                {
                    var note = file.items[i];
                    if (note == null) continue;

                    note.text = (note.text ?? string.Empty).Trim();
                    if (note.text.Length == 0) continue;

                    note.day = Mathf.Max(0, note.day);
                    note.time = Mathf.Clamp(note.time, 0, 1439);
                    note.type = IsType(note.type) ? note.type : Remember;
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

                var file = new CnNoteFile { items = _notes.ToArray() };
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
            Debug.LogWarning("[CN] заметки календаря: " + LastError + " (" + _file + ")");
        }

        private string NextId() => "n" + (++_seq).ToString();

        private static string Clean(string text)
        {
            string trimmed = (text ?? string.Empty).Trim();
            return trimmed.Length == 0 ? "Заметка" : trimmed;
        }
    }
}
