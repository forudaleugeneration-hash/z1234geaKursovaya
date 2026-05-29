using System.Data;                             // DataTable, DataRow — работа с результатами SQL-запросов
using System.Diagnostics;                      // Process — запуск внешней программы для открытия PDF
using LibraryApp.Database;                     // DatabaseHelper — выполнение запросов к базе данных
using LibraryApp.Services;                     // BookService, UserService, AdminService — бизнес-логика

namespace LibraryApp.Forms
{
    public partial class BookDetailForm : Form // partial — класс разделён на логику и дизайн; Form — окно Windows
    {
        private readonly int _bookId;            // ID просматриваемой книги
        private readonly int _userId;            // ID текущего пользователя
        private readonly BookService _bookService;   // Сервис для получения книги по ID, рейтинга
        private readonly UserService _userService;   // Сервис для закладок, бронирования, отзывов
        private readonly AdminService _adminService; // Сервис для загрузки обложек и PDF из БД
        private Models.Book? _book;               // Кешированный объект книги; ? — может быть null

        public BookDetailForm(int bookId, int userId) // Конструктор: bookId — какую книгу показать, userId — кто смотрит
        {
            InitializeComponent();               // Создаёт все элементы формы (метод из Designer.cs)
            _bookId = bookId;                    // Сохраняем ID книги
            _userId = userId;                    // Сохраняем ID пользователя
            _bookService = new BookService();    // Создаём сервис книг
            _userService = new UserService();    // Создаём сервис пользователей
            _adminService = new AdminService();  // Создаём сервис администрирования

            SetupEvents();                       // Подписываемся на клики кнопок
            LoadBookDetails();                   // Загружаем информацию о книге
            LoadReviews();                       // Загружаем отзывы
            CheckLoanStatus();                   // Проверяем, не взял ли пользователь уже эту книгу
        }

        private void SetupEvents()               // Подписка на события кнопок
        {
            btnAddToBookmarks.Click += (s, e) =>  // Кнопка «🔖 В закладки»
            {
                _userService.AddBookmark(_userId, _bookId); // Добавляем в закладки (SQL: INSERT INTO Bookmarks)
                MessageBox.Show("Книга добавлена в закладки!", "Успешно",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            btnRentBook.Click += (s, e) => RentBook();   // Кнопка «Забронировать» / «Взять онлайн»
            btnReadOnline.Click += (s, e) => ReadOnline(); // Кнопка «Читать онлайн»

            btnAddReview.Click += (s, e) =>       // Кнопка «✍ Отправить отзыв»
            {
                int rating = (int)numericRating.Value;   // Оценка (decimal → int)
                string comment = richTextBoxReview.Text; // Текст отзыва
                if (string.IsNullOrWhiteSpace(comment))  // Пустой отзыв не принимаем
                {
                    MessageBox.Show("Напишите отзыв.", "Предупреждение",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
                }
                _userService.AddReview(_userId, _bookId, rating, comment); // Сохраняем отзыв в БД
                MessageBox.Show("Отзыв добавлен!", "Успешно",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                richTextBoxReview.Clear();         // Очищаем поле ввода
                numericRating.Value = 5;           // Сбрасываем оценку на 5
                LoadReviews();                     // Обновляем таблицу отзывов
                LoadBookDetails();                 // Обновляем рейтинг книги
            };
        }

        private void LoadBookDetails()           // Загрузка и отображение информации о книге
        {
            _book = _bookService.GetBookById(_bookId); // Получаем объект Book из БД
            if (_book == null) return;            // Книга не найдена — выходим

            lblTitle.Text = _book.Title;          // Название (крупный шрифт)
            lblAuthor.Text = $"Автор: {_book.AuthorName}"; // Автор
            lblGenre.Text = $"Жанр: {_book.GenreName}";    // Жанр
            lblYear.Text = $"Год издания: {_book.PublicationYear}"; // Год
            lblLanguage.Text = $"Язык: {_book.Language}"; // Язык
            lblRating.Text = $"Рейтинг: {_book.Rating:F1} / 5.0"; // Рейтинг с одной цифрой после запятой
            lblType.Text = $"Тип: {(_book.BookType == "Онлайн" ? "📱 Онлайн (PDF)" : "📖 Печатная")}"; // Тип книги

            if (_book.BookType == "Онлайн")       // Онлайн-книги всегда доступны
            {
                lblAvailability.Text = "✅ Всегда доступна";
                lblAvailability.ForeColor = Color.Green;
                btnRentBook.Enabled = true;
            }
            else                                  // Печатная книга
            {
                lblAvailability.Text = _book.AvailableCopies > 0 // Есть доступные копии?
                    ? $"✅ Доступно: {_book.AvailableCopies} из {_book.TotalCopies}" // «Доступно: 3 из 5»
                    : "❌ Нет в наличии";          // Все на руках
                lblAvailability.ForeColor = _book.AvailableCopies > 0 ? Color.Green : Color.Red;
                btnRentBook.Enabled = _book.AvailableCopies > 0; // Активна только если есть копии
            }

            richTextBoxAnnotation.Text = _book.Annotation; // Аннотация
            btnReadOnline.Visible = _book.HasPdf;   // Кнопка «Читать» видна только для книг с PDF
            btnReadOnline.Enabled = false;           // Изначально неактивна
            btnReadOnline.Text = "🔒 Читать (после получения)"; // Замок — заблокировано
            btnReadOnline.BackColor = Color.Gray;    // Серый цвет
            btnRentBook.Text = _book.BookType == "Онлайн"
                ? "📱 Взять онлайн (30 дней)"        // Онлайн на 30 дней
                : "📖 Забронировать (код выдачи)";   // Печатная с кодом

            LoadCover();                             // Загружаем обложку
        }

        private void LoadCover()                 // Загрузка обложки из БД
        {
            pictureBoxCover.Controls.Clear();    // Удаляем старую заглушку
            var coverData = _adminService.GetBookCover(_bookId); // Массив байтов обложки
            if (coverData != null && coverData.Length > 0) // Обложка существует
            {
                try
                {
                    using (var ms = new MemoryStream(coverData)) // Поток в памяти
                    {
                        pictureBoxCover.Image = Image.FromStream(ms); // Показываем картинку
                    }
                }
                catch { ShowCoverPlaceholder(); } // Ошибка — заглушка
            }
            else ShowCoverPlaceholder();         // Нет обложки — заглушка
        }

        private void ShowCoverPlaceholder()      // Синяя заглушка с иконкой 📚
        {
            pictureBoxCover.Image = null;        // Убираем изображение
            pictureBoxCover.BackColor = Color.FromArgb(52, 152, 219); // Синий фон
            var lbl = new Label
            {
                Text = "📚",
                Font = new Font("Microsoft Sans Serif", 48),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            pictureBoxCover.Controls.Add(lbl);    // Добавляем иконку
        }

        private void CheckLoanStatus()           // Проверка: не взял ли пользователь уже эту книгу
        {
            string query = $@"SELECT l.LoanId, l.IsOnline, l.Status, l.DueDate, l.ReservationCode 
                             FROM Loans l 
                             WHERE l.UserId = {_userId} AND l.BookId = {_bookId} 
                             AND l.Status IN (N'Выдана', N'Забронирована')"; // Только активные статусы
            var dt = DatabaseHelper.ExecuteQuery(query); // Выполняем запрос
            if (dt.Rows.Count > 0)               // Книга уже взята или забронирована
            {
                bool isOnline = Convert.ToBoolean(dt.Rows[0]["IsOnline"]); // 0/1 → false/true
                string status = dt.Rows[0]["Status"]?.ToString() ?? "";    // «Выдана» или «Забронирована»
                DateTime dueDate = Convert.ToDateTime(dt.Rows[0]["DueDate"]); // Дата возврата
                string code = dt.Rows[0]["ReservationCode"]?.ToString() ?? ""; // Код брони

                btnRentBook.Enabled = false;     // Блокируем повторное взятие

                if (isOnline && status == "Выдана") // Онлайн-книга у пользователя
                {
                    btnRentBook.Text = "✅ Уже у вас";
                    btnReadOnline.Visible = true;
                    btnReadOnline.Enabled = true;    // Можно читать
                    btnReadOnline.Text = $"📖 Читать (до {dueDate:dd.MM.yyyy})"; // Дата окончания
                    btnReadOnline.BackColor = Color.FromArgb(46, 204, 113); // Зелёный
                }
                else if (status == "Забронирована") // Печатная книга забронирована
                {
                    btnRentBook.Text = "✅ Забронирована";
                    btnReadOnline.Visible = _book?.HasPdf ?? false; // Кнопка только для PDF
                    btnReadOnline.Enabled = false;    // Неактивна до выдачи
                    btnReadOnline.Text = "🔒 Читать (после выдачи)";
                    btnReadOnline.BackColor = Color.Gray;
                    panelReservationCode.Visible = true; // Показываем панель с кодом
                    lblReservationCode.Text = $"Код выдачи: {code}"; // 8-значный код
                    lblReservationInfo.Text = $"Предъявите код библиотекарю.\nСрок до {dueDate:dd.MM.yyyy}";
                }
            }
            else                                 // Книга не взята
            {
                btnReadOnline.Visible = _book?.HasPdf ?? false; // Кнопка только для PDF
                btnReadOnline.Enabled = false;
                btnReadOnline.Text = "🔒 Читать (после получения)";
                btnReadOnline.BackColor = Color.Gray;
            }
        }

        private void RentBook()                  // Бронирование / взятие книги
        {
            if (_book == null) return;           // Книга не загружена
            string result = _userService.BorrowBook(_userId, _bookId); // Пытаемся взять: "ONLINE", код, "NO_COPIES"

            if (result == "NO_COPIES")           // Все копии на руках
            {
                MessageBox.Show("К сожалению, все экземпляры книги на руках.\nПопробуйте позже.",
                    "Нет в наличии", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _bookService.IncrementPopularity(_bookId); // +1 к счётчику популярности

            if (result == "ONLINE")              // Онлайн-книга взята
            {
                MessageBox.Show("Книга взята онлайн на 30 дней!", "Успешно",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (!string.IsNullOrEmpty(result)) // Печатная — получен код
            {
                panelReservationCode.Visible = true; // Показываем панель с кодом
                lblReservationCode.Text = $"Код выдачи: {result}"; // result = 8-значный код
                lblReservationInfo.Text = "Предъявите код библиотекарю.\nСрок бронирования: 14 дней.";
                MessageBox.Show($"Книга забронирована!\n\nКод выдачи: {result}\n\nПредъявите библиотекарю.",
                    "Бронирование", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            LoadBookDetails();                   // Обновляем информацию
            CheckLoanStatus();                   // Обновляем статус кнопок
        }

        private void ReadOnline()                // Открытие PDF во внешней программе
        {
            var pdfData = _adminService.GetBookPdf(_bookId); // Массив байтов PDF
            if (pdfData == null || pdfData.Length == 0)     // PDF не найден
            { MessageBox.Show("PDF файл не найден.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), $"book_{_bookId}.pdf"); // Временный файл
                File.WriteAllBytes(tempPath, pdfData); // Сохраняем PDF на диск
                Process.Start(new ProcessStartInfo { FileName = tempPath, UseShellExecute = true }); // Открываем
            }
            catch (Exception ex)                 // Ошибка
            { MessageBox.Show($"Ошибка открытия PDF: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void LoadReviews()               // Загрузка отзывов в таблицу
        {
            string query = $@"SELECT r.Rating, r.Comment, r.ReviewDate, u.Username 
                             FROM Reviews r INNER JOIN Users u ON r.UserId = u.UserId 
                             WHERE r.BookId = {_bookId} ORDER BY r.ReviewDate DESC"; // Новые сверху
            var reviews = DatabaseHelper.ExecuteQuery(query); // DataTable с отзывами

            dataGridViewReviews.Rows.Clear();    // Очищаем старые строки

            foreach (DataRow row in reviews.Rows) // Перебираем строки результата
            {
                dataGridViewReviews.Rows.Add(     // Добавляем строку в таблицу
                    row["Rating"],                // [0] Оценка (1-5)
                    row["Comment"]?.ToString() ?? "", // [1] Текст отзыва
                    Convert.ToDateTime(row["ReviewDate"]).ToString("dd.MM.yyyy"), // [2] Дата
                    row["Username"]?.ToString() ?? "" // [3] Логин пользователя
                );
            }
        }
    }
}