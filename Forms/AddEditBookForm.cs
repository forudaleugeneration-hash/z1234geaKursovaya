using System.Data;                             // DataTable, DataRow — работа с результатами SQL-запросов
using LibraryApp.Database;                     // DatabaseHelper — выполнение запросов к базе данных
using LibraryApp.Models;                       // Book, User — модели данных
using LibraryApp.Services;                     // AdminService, BookService — сервисы бизнес-логики

namespace LibraryApp.Forms
{
    public partial class AddEditBookForm : Form // partial — класс разделён на логику и дизайн; Form — окно Windows
    {
        private readonly int? _bookId;           // int? — nullable: null = добавление, число = редактирование
        private readonly User _adminUser;        // Текущий админ/библиотекарь, выполняющий операцию
        private readonly AdminService _adminService; // Сервис для CRUD-операций с книгами, обложками, PDF
        private readonly BookService _bookService;   // Сервис для получения книги по ID
        private byte[]? _coverImageData;         // Массив байтов загруженной обложки; null — не выбрана
        private byte[]? _pdfData;                // Массив байтов загруженного PDF; null — не выбран

        public AddEditBookForm(int? bookId, User adminUser, AdminService adminService) // Конструктор
        {
            InitializeComponent();               // Создаёт все элементы формы (метод из Designer.cs)
            _bookId = bookId;                    // null — добавление, число — редактирование
            _adminUser = adminUser;              // Сохраняем для логирования
            _adminService = adminService;        // Сохраняем для операций с БД
            _bookService = new BookService();    // Создаём сервис книг (нужен только этой форме)

            SetupEvents();                       // Подписываемся на клики кнопок
            LoadAuthors();                       // Загружаем список авторов в выпадающий список
            LoadGenres();                        // Загружаем список жанров в выпадающий список

            if (_bookId.HasValue)                // HasValue — true если _bookId содержит число (не null)
            {
                Text = "Редактирование книги";   // Меняем заголовок окна
                LoadBookData();                  // Заполняем поля данными существующей книги
            }
            else
            {
                Text = "Добавление новой книги"; // Меняем заголовок окна
                comboBoxLanguage.SelectedIndex = 0; // Выбираем "Русский" по умолчанию
            }
        }

        private void SetupEvents()               // Подписка на события кнопок
        {
            btnLoadCover.Click += (s, e) => LoadCover();     // Клик по «Загрузить обложку»
            btnDeleteCover.Click += (s, e) => DeleteCover(); // Клик по «Удалить обложку»
            btnLoadPdf.Click += (s, e) => LoadPdf();         // Клик по «Загрузить PDF»
            btnDeletePdf.Click += (s, e) => DeletePdf();     // Клик по «Удалить PDF»
            btnSave.Click += (s, e) => SaveBook();           // Клик по «Сохранить»
            btnCancel.Click += (s, e) => Close();            // Клик по «Отмена» — закрыть форму
        }

        private void LoadAuthors()               // Загрузка списка авторов из БД
        {
            var authors = _adminService.GetAuthors();        // DataTable с авторами
            comboBoxAuthor.DataSource = authors;             // Привязываем к выпадающему списку
            comboBoxAuthor.DisplayMember = "AuthorName";     // Показываем имя автора
            comboBoxAuthor.ValueMember = "AuthorId";         // При выборе возвращаем ID
        }

        private void LoadGenres()                // Загрузка списка жанров из БД
        {
            var genres = _adminService.GetGenres();          // DataTable с жанрами
            comboBoxGenre.DataSource = genres;               // Привязываем к выпадающему списку
            comboBoxGenre.DisplayMember = "GenreName";       // Показываем название жанра
            comboBoxGenre.ValueMember = "GenreId";           // При выборе возвращаем ID
        }

        private void LoadBookData()              // Заполнение полей при редактировании существующей книги
        {
            var book = _bookService.GetBookById(_bookId!.Value); // ! — уверены что не null; получаем Book по ID
            if (book == null) return;            // Книга не найдена — выходим

            textBoxTitle.Text = book.Title;      // Название
            comboBoxAuthor.SelectedValue = book.AuthorId; // Выбираем автора по ID
            comboBoxGenre.SelectedValue = book.GenreId;   // Выбираем жанр по ID
            textBoxYear.Text = book.PublicationYear.ToString(); // Год (число → строка)
            comboBoxLanguage.SelectedItem = book.Language; // Язык
            richTextBoxAnnotation.Text = book.Annotation; // Аннотация
            checkBoxIsNew.Checked = book.IsNew;  // Галочка «Новинка»
            checkBoxIsHit.Checked = book.IsHit;  // Галочка «Хит чтения»
            checkBoxIsOnline.Checked = book.BookType == "Онлайн"; // Галочка «Онлайн»
            numericTotalCopies.Value = book.TotalCopies; // Количество копий

            var coverData = _adminService.GetBookCover(_bookId.Value); // Загружаем обложку из БД
            if (coverData != null && coverData.Length > 0) // Обложка существует
            {
                _coverImageData = coverData;     // Сохраняем в поле класса
                using (var ms = new MemoryStream(coverData)) // Поток в памяти из байтов
                {
                    pictureBoxCover.Image = Image.FromStream(ms); // Показываем картинку
                }
                pictureBoxCover.SizeMode = PictureBoxSizeMode.Zoom; // Масштабирование
                lblCoverStatus.Text = "Обложка загружена"; // Статус
                lblCoverStatus.ForeColor = Color.Green;  // Зелёный — есть
                btnDeleteCover.Visible = true; // Показываем кнопку удаления
            }

            var pdfData = _adminService.GetBookPdf(_bookId.Value); // Загружаем PDF из БД
            if (pdfData != null && pdfData.Length > 0) // PDF существует
            {
                _pdfData = pdfData;              // Сохраняем в поле класса
                lblPdfStatus.Text = "PDF загружен"; // Статус
                lblPdfStatus.ForeColor = Color.Green; // Зелёный — есть
                btnDeletePdf.Visible = true;     // Показываем кнопку удаления
            }
        }

        private void LoadCover()                 // Выбор файла обложки с компьютера
        {
            using var openFileDialog = new OpenFileDialog // Стандартное окно выбора файла
            {
                Title = "Выберите обложку книги", // Заголовок окна
                Filter = "Изображения (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif" // Фильтр
            };
            if (openFileDialog.ShowDialog() == DialogResult.OK) // Пользователь нажал «Открыть»
            {
                try                          // Блок для перехвата ошибок
                {
                    _coverImageData = File.ReadAllBytes(openFileDialog.FileName); // Читаем файл в массив байтов
                    using (var ms = new MemoryStream(_coverImageData)) // Поток в памяти
                    {
                        pictureBoxCover.Image = Image.FromStream(ms); // Показываем картинку
                    }
                    pictureBoxCover.SizeMode = PictureBoxSizeMode.Zoom; // Масштабирование
                    lblCoverStatus.Text = Path.GetFileName(openFileDialog.FileName); // Только имя файла
                    lblCoverStatus.ForeColor = Color.Green; // Зелёный — успешно
                    btnDeleteCover.Visible = true; // Показываем кнопку удаления
                }
                catch (Exception ex)         // Если ошибка
                { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void DeleteCover()               // Удаление обложки
        {
            if (MessageBox.Show("Удалить обложку?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _coverImageData = null;          // Обнуляем данные в памяти
                pictureBoxCover.Image = null;    // Убираем картинку
                lblCoverStatus.Text = "Обложка не загружена"; // Статус
                lblCoverStatus.ForeColor = Color.Gray; // Серый — нейтрально
                btnDeleteCover.Visible = false;  // Скрываем кнопку
                if (_bookId.HasValue)            // Если редактируем существующую
                    _adminService.DeleteBookCover(_bookId.Value); // Удаляем из БД
            }
        }

        private void LoadPdf()                   // Выбор PDF файла с компьютера
        {
            using var openFileDialog = new OpenFileDialog
            {
                Title = "Выберите PDF файл книги",
                Filter = "PDF файлы (*.pdf)|*.pdf" // Только PDF
            };
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _pdfData = File.ReadAllBytes(openFileDialog.FileName); // Читаем PDF в массив байтов
                    lblPdfStatus.Text = Path.GetFileName(openFileDialog.FileName); // Имя файла
                    lblPdfStatus.ForeColor = Color.Green; // Зелёный — успешно
                    btnDeletePdf.Visible = true; // Показываем кнопку удаления
                    checkBoxIsOnline.Checked = true; // Автоматически отмечаем как онлайн
                }
                catch (Exception ex)
                { MessageBox.Show($"Ошибка загрузки PDF: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void DeletePdf()                 // Удаление PDF
        {
            if (MessageBox.Show("Удалить PDF файл?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _pdfData = null;                 // Обнуляем данные
                lblPdfStatus.Text = "PDF не загружен"; // Статус
                lblPdfStatus.ForeColor = Color.Gray; // Серый
                btnDeletePdf.Visible = false;    // Скрываем кнопку
                checkBoxIsOnline.Checked = false; // Снимаем галочку «Онлайн»
                if (_bookId.HasValue)            // Если редактируем существующую
                    _adminService.DeleteBookPdf(_bookId.Value); // Удаляем из БД
            }
        }

        private void SaveBook()                  // Сохранение книги (добавление или обновление)
        {
            if (string.IsNullOrWhiteSpace(textBoxTitle.Text)) // Пустое название?
            { MessageBox.Show("Введите название книги.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (comboBoxAuthor.SelectedValue == null) // Автор не выбран?
            { MessageBox.Show("Выберите автора.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (comboBoxGenre.SelectedValue == null) // Жанр не выбран?
            { MessageBox.Show("Выберите жанр.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (!int.TryParse(textBoxYear.Text, out int year)) // Год не число?
            { MessageBox.Show("Введите корректный год.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            string title = textBoxTitle.Text;    // Название
            int authorId = (int)comboBoxAuthor.SelectedValue; // ID автора (object → int)
            int genreId = (int)comboBoxGenre.SelectedValue;   // ID жанра
            string language = comboBoxLanguage.SelectedItem?.ToString() ?? "Русский"; // ?. — если не null; ?? — иначе "Русский"
            string annotation = richTextBoxAnnotation.Text; // Аннотация
            bool isNew = checkBoxIsNew.Checked;  // Новинка
            bool isHit = checkBoxIsHit.Checked;  // Хит
            bool isOnline = checkBoxIsOnline.Checked || _pdfData != null; // Онлайн если галочка ИЛИ загружен PDF
            string bookType = isOnline ? "Онлайн" : "Печатная"; // Тернарный оператор
            int totalCopies = (int)numericTotalCopies.Value; // Количество копий (decimal → int)

            if (_bookId.HasValue)                // РЕДАКТИРОВАНИЕ существующей книги
            {
                _adminService.UpdateBook(_bookId.Value, title, authorId, genreId, year, language, annotation, isNew, isHit, bookType, totalCopies);
                if (_coverImageData != null)     // Загружена новая обложка
                    _adminService.UpdateBookCover(_bookId.Value, _coverImageData); // Сохраняем в БД
                if (_pdfData != null)            // Загружен новый PDF
                    _adminService.UpdateBookPdf(_bookId.Value, _pdfData); // Сохраняем в БД
                else if (!isOnline)              // PDF не загружен И книга печатная
                    _adminService.DeleteBookPdf(_bookId.Value); // Удаляем старый PDF
                _adminService.LogAction(_adminUser.UserId, "Редактирование книги", title); // Запись в лог
            }
            else                                 // ДОБАВЛЕНИЕ новой книги
            {
                _adminService.AddBook(title, authorId, genreId, year, language, annotation, totalCopies); // INSERT
                var result = DatabaseHelper.ExecuteScalar("SELECT MAX(BookId) FROM Books"); // ID новой книги
                if (result != null && result != DBNull.Value) // Если получили ID
                {
                    int newBookId = Convert.ToInt32(result); // object → int
                    if (_coverImageData != null) // Загружена обложка
                        _adminService.UpdateBookCover(newBookId, _coverImageData); // Сохраняем
                    if (_pdfData != null)        // Загружен PDF
                        _adminService.UpdateBookPdf(newBookId, _pdfData); // Сохраняем
                }
                _adminService.LogAction(_adminUser.UserId, "Добавление книги", title); // Запись в лог
            }

            DialogResult = DialogResult.OK;      // Сигнал: операция успешна
            Close();                             // Закрываем форму
        }
    }
}