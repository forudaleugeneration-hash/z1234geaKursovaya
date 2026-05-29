using System.Data;                             // DataTable, DataRow — работа с табличными данными из БД
using Microsoft.Data.SqlClient;                // SqlCommand — для параметризованных запросов (обложки, PDF)
using LibraryApp.Database;                     // DatabaseHelper — выполнение SQL-запросов
using LibraryApp.Models;                       // UserWithRole, BookManageModel, BookPickup — модели данных

namespace LibraryApp.Services
{
    public class AdminService                  // Сервис для ВСЕХ административных операций (CRUD + бронирование + выдача)
    {

        // ПОЛЬЗОВАТЕЛИ


        public List<UserWithRole> GetAllUsers()  // Получить список всех пользователей с ролями и статистикой
        {
            var users = new List<UserWithRole>(); // Создаём пустой список для заполнения
            // u.* — все колонки из таблицы Users (UserId, Username, Email, FullName, IsActive, RegistrationDate)
            // r.RoleName — название роли из таблицы Roles (подтягивается через JOIN)
            // (SELECT COUNT(*) FROM Loans WHERE UserId = u.UserId) — подзапрос: сколько у пользователя займов
            // (SELECT COUNT(*) FROM Reviews WHERE UserId = u.UserId) — подзапрос: сколько у пользователя отзывов
            // INNER JOIN Roles r ON u.RoleId = r.RoleId — соединяем Users с Roles по RoleId для получения названия роли
            // ORDER BY u.RegistrationDate DESC — сортировка: новые пользователи сверху
            string query = @"SELECT u.*, r.RoleName,
                            (SELECT COUNT(*) FROM Loans WHERE UserId = u.UserId) as LoansCount,
                            (SELECT COUNT(*) FROM Reviews WHERE UserId = u.UserId) as ReviewsCount
                            FROM Users u 
                            INNER JOIN Roles r ON u.RoleId = r.RoleId 
                            ORDER BY u.RegistrationDate DESC";
            var dt = DatabaseHelper.ExecuteQuery(query); // Выполняем запрос, получаем DataTable
            foreach (DataRow row in dt.Rows)             // Перебираем строки результата
            {
                users.Add(new UserWithRole               // Создаём объект UserWithRole из строки таблицы
                {
                    UserId = Convert.ToInt32(row["UserId"]),              // object → int (ID пользователя)
                    Username = row["Username"]?.ToString() ?? string.Empty, // object → string (логин)
                    Email = row["Email"]?.ToString() ?? string.Empty,      // object → string (почта)
                    FullName = row["FullName"]?.ToString() ?? string.Empty, // object → string (полное имя)
                    RegistrationDate = Convert.ToDateTime(row["RegistrationDate"]), // object → DateTime (дата регистрации)
                    IsActive = Convert.ToBoolean(row["IsActive"]),       // object → bool (активен/заблокирован)
                    RoleName = row["RoleName"]?.ToString() ?? string.Empty, // object → string (название роли из JOIN)
                    LoansCount = Convert.ToInt32(row["LoansCount"]),     // object → int (количество займов из подзапроса)
                    ReviewsCount = Convert.ToInt32(row["ReviewsCount"])  // object → int (количество отзывов из подзапроса)
                });
            }
            return users;                        // Возвращаем готовый список
        }

        public void UpdateUserRole(int userId, int roleId) // Изменить роль пользователя (1=Admin, 2=Librarian, 3=User)
        {
            // UPDATE Users SET RoleId = {roleId} — обновляем роль; WHERE UserId = {userId} — только для конкретного пользователя
            DatabaseHelper.ExecuteNonQuery($"UPDATE Users SET RoleId = {roleId} WHERE UserId = {userId}");
        }

        public void ToggleUserActive(int userId, bool isActive) // Переключить активность (заблокировать/разблокировать)
        {
            // {(isActive ? 1 : 0)} — тернарный оператор: true → 1 (активен), false → 0 (заблокирован)
            DatabaseHelper.ExecuteNonQuery($"UPDATE Users SET IsActive = {(isActive ? 1 : 0)} WHERE UserId = {userId}");
        }

        public void DeleteUser(int userId)       // Полное удаление пользователя и всех его данных
        {
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM Reviews WHERE UserId = {userId}");      // 1. Удаляем его отзывы
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM Bookmarks WHERE UserId = {userId}");    // 2. Удаляем его закладки
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM ReadHistory WHERE UserId = {userId}");  // 3. Удаляем историю чтения
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM BookPickups WHERE UserId = {userId}"); // 4. Удаляем записи выдачи
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM Loans WHERE UserId = {userId}");        // 5. Удаляем его займы
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM AdminLogs WHERE AdminId = {userId}");   // 6. Удаляем его логи (если был админом)
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM Users WHERE UserId = {userId} AND RoleId != 1"); // 7. Удаляем самого (но не админа)
        }


        // КНИГИ


        public List<BookManageModel> GetAllBooksForManagement() // Получить список книг с доп. информацией для админ-панели
        {
            var books = new List<BookManageModel>();
            // b.* — все колонки из таблицы Books
            // a.AuthorName — имя автора из таблицы Authors (через JOIN)
            // g.GenreName — название жанра из таблицы Genres (через JOIN)
            // (SELECT COUNT(*) FROM Loans WHERE BookId = b.BookId) — подзапрос: сколько раз книгу брали
            // (SELECT COUNT(*) FROM Reviews WHERE BookId = b.BookId) — подзапрос: сколько у книги отзывов
            // INNER JOIN Authors a ON b.AuthorId = a.AuthorId — соединяем Books с Authors
            // INNER JOIN Genres g ON b.GenreId = g.GenreId — соединяем Books с Genres
            // ORDER BY b.BookId — сортировка по ID книги
            string query = @"SELECT b.*, a.AuthorName, g.GenreName,
                            (SELECT COUNT(*) FROM Loans WHERE BookId = b.BookId) as LoansCount,
                            (SELECT COUNT(*) FROM Reviews WHERE BookId = b.BookId) as ReviewsCount
                            FROM Books b 
                            INNER JOIN Authors a ON b.AuthorId = a.AuthorId 
                            INNER JOIN Genres g ON b.GenreId = g.GenreId 
                            ORDER BY b.BookId";
            var dt = DatabaseHelper.ExecuteQuery(query);
            foreach (DataRow row in dt.Rows)
            {
                books.Add(new BookManageModel
                {
                    BookId = Convert.ToInt32(row["BookId"]),
                    Title = row["Title"]?.ToString() ?? string.Empty,
                    AuthorId = Convert.ToInt32(row["AuthorId"]),
                    AuthorName = row["AuthorName"]?.ToString() ?? string.Empty,          // Из JOIN с Authors
                    GenreId = Convert.ToInt32(row["GenreId"]),
                    GenreName = row["GenreName"]?.ToString() ?? string.Empty,            // Из JOIN с Genres
                    PublicationYear = Convert.ToInt32(row["PublicationYear"]),
                    Language = row["Language"]?.ToString() ?? "Русский",
                    Annotation = row["Annotation"]?.ToString() ?? string.Empty,
                    Rating = Convert.ToDecimal(row["Rating"]),                           // decimal — дробный рейтинг
                    Popularity = Convert.ToInt32(row["Popularity"]),
                    IsAvailable = Convert.ToBoolean(row["IsAvailable"]),
                    IsNew = Convert.ToBoolean(row["IsNew"]),
                    IsHit = Convert.ToBoolean(row["IsHit"]),
                    DateAdded = Convert.ToDateTime(row["DateAdded"]),
                    BookType = row.Table.Columns.Contains("BookType")                    // Проверка: есть ли колонка BookType
                        ? row["BookType"]?.ToString() ?? "Печатная"                     // Есть → берём значение
                        : "Печатная",                                                    // Нет → по умолчанию "Печатная"
                    TotalCopies = row.Table.Columns.Contains("TotalCopies")              // Проверка: есть ли колонка TotalCopies
                        ? Convert.ToInt32(row["TotalCopies"]) : 1,                       // Есть → берём, нет → 1
                    AvailableCopies = row.Table.Columns.Contains("AvailableCopies")      // Проверка: есть ли колонка AvailableCopies
                        ? Convert.ToInt32(row["AvailableCopies"]) : 1,                   // Есть → берём, нет → 1
                    LoansCount = Convert.ToInt32(row["LoansCount"]),                     // Из подзапроса (сколько выдач)
                    ReviewsCount = Convert.ToInt32(row["ReviewsCount"])                  // Из подзапроса (сколько отзывов)
                });
            }
            return books;
        }

        public void AddBook(string title, int authorId, int genreId, int year,
            string language, string annotation, int totalCopies = 1) // Добавить новую книгу; totalCopies=1 по умолчанию
        {
            // INSERT INTO Books (...) — вставляем новую строку в таблицу Books
            // N'...' — Unicode (NVARCHAR) для русских букв
            // Replace("'","''") — экранирование кавычек для безопасности SQL
            // AvailableCopies = TotalCopies — при создании все копии доступны
            string query = $@"INSERT INTO Books (Title, AuthorId, GenreId, PublicationYear, Language, Annotation, TotalCopies, AvailableCopies) 
                             VALUES (N'{title.Replace("'", "''")}', {authorId}, {genreId}, {year}, 
                                     N'{language}', N'{annotation.Replace("'", "''")}', {totalCopies}, {totalCopies})";
            DatabaseHelper.ExecuteNonQuery(query); // Выполняем INSERT (не возвращает строки)
        }

        public void UpdateBook(int bookId, string title, int authorId, int genreId, int year,
            string language, string annotation, bool isNew, bool isHit,
            string bookType = "Печатная", int totalCopies = 1) // Обновить существующую книгу
        {
            // UPDATE Books SET ... — обновляем все поля книги
            // {(isNew ? 1 : 0)} — bool → 1/0 для SQL (BIT)
            // WHERE BookId = {bookId} — только конкретную книгу
            string query = $@"UPDATE Books SET 
                             Title = N'{title.Replace("'", "''")}',
                             AuthorId = {authorId}, GenreId = {genreId},
                             PublicationYear = {year}, Language = N'{language}',
                             Annotation = N'{annotation.Replace("'", "''")}',
                             IsNew = {(isNew ? 1 : 0)}, IsHit = {(isHit ? 1 : 0)},
                             BookType = N'{bookType}', TotalCopies = {totalCopies}
                             WHERE BookId = {bookId}";
            DatabaseHelper.ExecuteNonQuery(query);
        }

        public void DeleteBook(int bookId)       // Удалить книгу и все связанные данные
        {
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM Reviews WHERE BookId = {bookId}");       // 1. Отзывы о книге
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM Bookmarks WHERE BookId = {bookId}");     // 2. Закладки на книгу
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM ReadHistory WHERE BookId = {bookId}");   // 3. История чтения
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM BookPickups WHERE BookId = {bookId}");  // 4. Записи выдачи
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM Loans WHERE BookId = {bookId}");         // 5. Займы книги
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM Books WHERE BookId = {bookId}");         // 6. Саму книгу
        }


        // АВТОРЫ


        public void AddAuthor(string authorName) // Добавить нового автора
        {
            // INSERT INTO Authors (AuthorName) VALUES (...) — добавляем одну строку
            DatabaseHelper.ExecuteNonQuery($"INSERT INTO Authors (AuthorName) VALUES (N'{authorName.Replace("'", "''")}')");
        }

        public void UpdateAuthor(int authorId, string authorName) // Переименовать автора
        {
            // UPDATE Authors SET AuthorName = ... WHERE AuthorId = ... — обновляем имя конкретного автора
            DatabaseHelper.ExecuteNonQuery($"UPDATE Authors SET AuthorName = N'{authorName.Replace("'", "''")}' WHERE AuthorId = {authorId}");
        }

        public bool DeleteAuthor(int authorId)   // Удалить автора; возвращает true если успешно, false если есть книги
        {
            // SELECT COUNT(*) FROM Books WHERE AuthorId = ... — проверяем, есть ли книги у этого автора
            var result = DatabaseHelper.ExecuteScalar($"SELECT COUNT(*) FROM Books WHERE AuthorId = {authorId}");
            if (result != null && Convert.ToInt32(result) > 0) // Если у автора есть книги
                return false;                      // Возвращаем false — нельзя удалить
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM Authors WHERE AuthorId = {authorId}"); // Удаляем автора
            return true;                           // Возвращаем true — успешно удалён
        }

        public DataTable GetAuthors()            // Получить список всех авторов (для выпадающих списков)
        {
            // SELECT * FROM Authors ORDER BY AuthorName — все авторы, сортировка по алфавиту
            return DatabaseHelper.ExecuteQuery("SELECT * FROM Authors ORDER BY AuthorName");
        }

        public DataTable GetAuthorsWithBookCount() // Получить авторов с количеством книг (для админ-панели)
        {
            // a.AuthorId, a.AuthorName — данные автора
            // COUNT(b.BookId) as BookCount — подсчёт количества книг автора
            // LEFT JOIN Books b ON a.AuthorId = b.AuthorId — соединяем с книгами; LEFT — даже если книг нет
            // GROUP BY a.AuthorId, a.AuthorName — группировка для COUNT
            // ORDER BY a.AuthorName — сортировка по алфавиту
            return DatabaseHelper.ExecuteQuery(@"SELECT a.AuthorId, a.AuthorName, COUNT(b.BookId) as BookCount
                                                FROM Authors a LEFT JOIN Books b ON a.AuthorId = b.AuthorId 
                                                GROUP BY a.AuthorId, a.AuthorName ORDER BY a.AuthorName");
        }


        // ЖАНРЫ


        public void AddGenre(string genreName)   // Добавить новый жанр
        {
            DatabaseHelper.ExecuteNonQuery($"INSERT INTO Genres (GenreName) VALUES (N'{genreName.Replace("'", "''")}')");
        }

        public void UpdateGenre(int genreId, string genreName) // Переименовать жанр
        {
            DatabaseHelper.ExecuteNonQuery($"UPDATE Genres SET GenreName = N'{genreName.Replace("'", "''")}' WHERE GenreId = {genreId}");
        }

        public bool DeleteGenre(int genreId)     // Удалить жанр; возвращает true если успешно
        {
            var result = DatabaseHelper.ExecuteScalar($"SELECT COUNT(*) FROM Books WHERE GenreId = {genreId}"); // Сколько книг этого жанра
            if (result != null && Convert.ToInt32(result) > 0) return false; // Есть книги — нельзя удалить
            DatabaseHelper.ExecuteNonQuery($"DELETE FROM Genres WHERE GenreId = {genreId}"); // Удаляем жанр
            return true;
        }

        public DataTable GetGenres()             // Получить список всех жанров (для выпадающих списков)
        {
            return DatabaseHelper.ExecuteQuery("SELECT * FROM Genres ORDER BY GenreName");
        }

        public DataTable GetGenresWithBookCount() // Получить жанры с количеством книг (для админ-панели)
        {
            // Аналогично GetAuthorsWithBookCount, но для жанров
            // LEFT JOIN Books — включаем жанры без книг
            return DatabaseHelper.ExecuteQuery(@"SELECT g.GenreId, g.GenreName, COUNT(b.BookId) as BookCount
                                                FROM Genres g LEFT JOIN Books b ON g.GenreId = b.GenreId 
                                                GROUP BY g.GenreId, g.GenreName ORDER BY g.GenreName");
        }


        // ОБЛОЖКИ (параметризованные запросы для BLOB-данных)


        public void UpdateBookCover(int bookId, byte[] coverImage) // Сохранить/обновить обложку
        {
            string query = "UPDATE Books SET CoverImage = @CoverImage WHERE BookId = @BookId"; // @Параметры — защита от инъекций
            using var connection = DatabaseHelper.GetConnection();  // Открываем соединение; using — авто-закрытие
            using var command = new SqlCommand(query, connection);  // SqlCommand — параметризованная SQL-команда
            command.Parameters.AddWithValue("@CoverImage", coverImage); // Подставляем массив байтов в @CoverImage
            command.Parameters.AddWithValue("@BookId", bookId);     // Подставляем ID книги в @BookId
            command.ExecuteNonQuery();                               // Выполняем UPDATE
        }

        public byte[]? GetBookCover(int bookId)  // Получить обложку (массив байтов или null)
        {
            // SELECT CoverImage FROM Books WHERE BookId = ... — получаем одну ячейку
            var dt = DatabaseHelper.ExecuteQuery($"SELECT CoverImage FROM Books WHERE BookId = {bookId}");
            if (dt.Rows.Count > 0 && dt.Rows[0]["CoverImage"] != DBNull.Value) // Обложка существует и не NULL
                return (byte[])dt.Rows[0]["CoverImage"];         // Приведение object → byte[]
            return null;                                         // Обложки нет
        }

        public void DeleteBookCover(int bookId)  // Удалить обложку (установить NULL)
        {
            // UPDATE Books SET CoverImage = NULL — обнуляем ячейку
            DatabaseHelper.ExecuteNonQuery($"UPDATE Books SET CoverImage = NULL WHERE BookId = {bookId}");
        }


        // PDF


        public void UpdateBookPdf(int bookId, byte[] pdfContent) // Сохранить PDF и пометить книгу как «Онлайн»
        {
            string query = "UPDATE Books SET PdfContent = @PdfContent, BookType = N'Онлайн' WHERE BookId = @BookId";
            using var connection = DatabaseHelper.GetConnection();
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@PdfContent", pdfContent); // Массив байтов PDF
            command.Parameters.AddWithValue("@BookId", bookId);
            command.ExecuteNonQuery();
        }

        public byte[]? GetBookPdf(int bookId)    // Получить PDF книги
        {
            var dt = DatabaseHelper.ExecuteQuery($"SELECT PdfContent FROM Books WHERE BookId = {bookId}");
            if (dt.Rows.Count > 0 && dt.Rows[0]["PdfContent"] != DBNull.Value)
                return (byte[])dt.Rows[0]["PdfContent"];
            return null;
        }

        public void DeleteBookPdf(int bookId)    // Удалить PDF и вернуть тип «Печатная»
        {
            DatabaseHelper.ExecuteNonQuery($"UPDATE Books SET PdfContent = NULL, BookType = N'Печатная' WHERE BookId = {bookId}");
        }

        public bool HasPdf(int bookId)           // Проверить, есть ли у книги PDF
        {
            var dt = DatabaseHelper.ExecuteQuery($"SELECT PdfContent FROM Books WHERE BookId = {bookId}");
            return dt.Rows.Count > 0 && dt.Rows[0]["PdfContent"] != DBNull.Value; // true если PDF есть и не NULL
        }


        // БРОНИРОВАНИЕ И ВЫДАЧА КНИГ


        public List<BookPickup> GetPendingPickups() // Получить все записи о выдаче/бронировании
        {
            var pickups = new List<BookPickup>();
            // bp.* — все колонки из BookPickups
            // l.LoanDate — дата займа из Loans (через JOIN)
            // b.Title as BookTitle — название книги из Books (через JOIN)
            // u.Username as UserName — логин читателя из Users (через JOIN)
            // lib.FullName as LibrarianName — имя библиотекаря из Users (через LEFT JOIN — может быть NULL)
            // INNER JOIN Loans — у каждой записи выдачи есть займ
            // INNER JOIN Books — у каждой записи есть книга
            // INNER JOIN Users u — у каждой записи есть читатель
            // LEFT JOIN Users lib — библиотекарь может быть ещё не назначен (NULL)
            // ORDER BY CASE ... — своя сортировка: Ожидает → Выдана → Возвращена, внутри — по дате займа
            string query = @"SELECT bp.*, l.LoanDate, b.Title as BookTitle, u.Username as UserName,
                            lib.FullName as LibrarianName
                            FROM BookPickups bp 
                            INNER JOIN Loans l ON bp.LoanId = l.LoanId 
                            INNER JOIN Books b ON bp.BookId = b.BookId 
                            INNER JOIN Users u ON bp.UserId = u.UserId 
                            LEFT JOIN Users lib ON bp.LibrarianId = lib.UserId 
                            ORDER BY CASE bp.Status WHEN N'Ожидает' THEN 1 WHEN N'Выдана' THEN 2 WHEN N'Возвращена' THEN 3 ELSE 4 END, 
                            l.LoanDate DESC";
            var dt = DatabaseHelper.ExecuteQuery(query);
            foreach (DataRow row in dt.Rows)
            {
                pickups.Add(new BookPickup
                {
                    PickupId = Convert.ToInt32(row["PickupId"]),
                    LoanId = Convert.ToInt32(row["LoanId"]),
                    BookId = Convert.ToInt32(row["BookId"]),
                    UserId = Convert.ToInt32(row["UserId"]),
                    BookTitle = row["BookTitle"]?.ToString() ?? "",                  // Из JOIN с Books
                    UserName = row["UserName"]?.ToString() ?? "",                    // Из JOIN с Users (читатель)
                    ReservationCode = row["ReservationCode"]?.ToString() ?? "",
                    PickupDate = row["PickupDate"] != DBNull.Value                   // Если дата выдачи не NULL
                        ? Convert.ToDateTime(row["PickupDate"])                      // Преобразуем в DateTime
                        : null,                                                      // Иначе null (ещё не выдана)
                    LoanDate = Convert.ToDateTime(row["LoanDate"]),                  // Дата бронирования
                    Status = row["Status"]?.ToString() ?? "Ожидает",                 // Статус: Ожидает/Выдана/Возвращена
                    LibrarianName = row["LibrarianName"]?.ToString() ?? ""           // Из LEFT JOIN с Users (библиотекарь)
                });
            }
            return pickups;
        }

        public string GenerateReservationCode()  // Сгенерировать случайный 8-значный код
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 30 символов без O,0,I,1 (чтобы не путать)
            var random = new Random();                               // Генератор случайных чисел
            // Enumerable.Range(0,8) — 8 итераций; Select — для каждой берём случайный символ; ToArray — в массив; new string — в строку
            return new string(Enumerable.Range(0, 8).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }

        public string CreateReservation(int userId, int bookId) // Создать бронирование
        {
            var book = new BookService().GetBookById(bookId);        // Проверяем доступность книги
            if (book == null) return string.Empty;                   // Книга не найдена
            if (book.BookType == "Печатная" && book.AvailableCopies <= 0) // Печатная и нет копий
                return "NO_COPIES";                                  // Специальный ответ: нет в наличии

            string code = GenerateReservationCode();                 // Генерируем код
            // Проверяем уникальность кода (повторяем генерацию если такой уже есть)
            string checkQuery = $"SELECT COUNT(*) FROM Loans WHERE ReservationCode = '{code}'";
            while (Convert.ToInt32(DatabaseHelper.ExecuteScalar(checkQuery)) > 0)
                code = GenerateReservationCode();

            var dueDate = DateTime.Now.AddDays(14);                  // Дата возврата: сегодня + 14 дней
            // INSERT INTO Loans — создаём займ со статусом «Забронирована»
            string query = $@"INSERT INTO Loans (UserId, BookId, DueDate, Status, ReservationCode, IsOnline) 
                             VALUES ({userId}, {bookId}, '{dueDate:yyyy-MM-dd}', N'Забронирована', '{code}', 0)";
            DatabaseHelper.ExecuteNonQuery(query);

            // SELECT MAX(LoanId) — получаем ID только что созданного займа
            var result = DatabaseHelper.ExecuteScalar("SELECT MAX(LoanId) FROM Loans");
            int loanId = result != null ? Convert.ToInt32(result) : 0;

            // INSERT INTO BookPickups — создаём запись в таблице выдачи
            query = $@"INSERT INTO BookPickups (LoanId, BookId, UserId, ReservationCode) 
                      VALUES ({loanId}, {bookId}, {userId}, '{code}')";
            DatabaseHelper.ExecuteNonQuery(query);

            if (book.BookType == "Печатная")                         // Для печатных — уменьшаем счётчик доступных
            {
                DatabaseHelper.ExecuteNonQuery($"UPDATE Books SET AvailableCopies = AvailableCopies - 1 WHERE BookId = {bookId}");
                DatabaseHelper.ExecuteNonQuery($"UPDATE Books SET IsAvailable = 0 WHERE BookId = {bookId} AND AvailableCopies <= 0");
            }

            return code;                                             // Возвращаем код пользователю
        }

        public bool ConfirmPickup(int pickupId, int librarianId) // Подтвердить выдачу книги
        {
            // UPDATE BookPickups SET Status = 'Выдана', PickupDate = GETDATE(), LibrarianId = ...
            // WHERE PickupId = ... AND Status = 'Ожидает' — только если статус «Ожидает»
            string query = $@"UPDATE BookPickups 
                             SET Status = N'Выдана', PickupDate = GETDATE(), LibrarianId = {librarianId} 
                             WHERE PickupId = {pickupId} AND Status = N'Ожидает'";
            int rows = DatabaseHelper.ExecuteNonQuery(query);       // Сколько строк обновлено (0 или 1)
            if (rows > 0)                                            // Если обновили (статус был «Ожидает»)
            {
                // UPDATE Loans SET Status = 'Выдана' — обновляем статус связанного займа
                // WHERE LoanId = (SELECT LoanId FROM BookPickups WHERE PickupId = ...) — подзапрос
                query = $@"UPDATE Loans SET Status = N'Выдана' 
                          WHERE LoanId = (SELECT LoanId FROM BookPickups WHERE PickupId = {pickupId})";
                DatabaseHelper.ExecuteNonQuery(query);
            }
            return rows > 0;                                         // true — успешно, false — не найден
        }

        public bool ReturnBookByLibrarian(int pickupId, int librarianId) // Принять возврат книги
        {
            // UPDATE BookPickups SET Status = 'Возвращена' — обновляем статус выдачи
            string query = $@"UPDATE BookPickups 
                             SET Status = N'Возвращена', LibrarianId = {librarianId} 
                             WHERE PickupId = {pickupId} AND Status = N'Выдана'";
            int rows = DatabaseHelper.ExecuteNonQuery(query);
            if (rows > 0)
            {
                // UPDATE Loans SET Status = 'Возвращена', ReturnDate = GETDATE() — обновляем займ
                query = $@"UPDATE Loans SET Status = N'Возвращена', ReturnDate = GETDATE() 
                          WHERE LoanId = (SELECT LoanId FROM BookPickups WHERE PickupId = {pickupId})";
                DatabaseHelper.ExecuteNonQuery(query);

                // UPDATE Books SET AvailableCopies = AvailableCopies + 1 — возвращаем копию
                query = $@"UPDATE Books SET AvailableCopies = AvailableCopies + 1, IsAvailable = 1 
                          WHERE BookId = (SELECT BookId FROM BookPickups WHERE PickupId = {pickupId})";
                DatabaseHelper.ExecuteNonQuery(query);

                // INSERT INTO ReadHistory — добавляем в историю прочитанных (если ещё нет)
                // AND NOT EXISTS — проверка что записи ещё нет в истории
                query = $@"INSERT INTO ReadHistory (UserId, BookId)
                          SELECT UserId, BookId FROM BookPickups WHERE PickupId = {pickupId}
                          AND NOT EXISTS (SELECT 1 FROM ReadHistory WHERE UserId = BookPickups.UserId AND BookId = BookPickups.BookId)";
                DatabaseHelper.ExecuteNonQuery(query);
            }
            return rows > 0;
        }

        public bool CancelReservation(int pickupId, int librarianId) // Отменить бронирование
        {
            // SELECT LoanId, BookId FROM BookPickups — получаем данные для отмены
            string getInfoQuery = $@"SELECT LoanId, BookId FROM BookPickups WHERE PickupId = {pickupId} AND Status = N'Ожидает'";
            var dt = DatabaseHelper.ExecuteQuery(getInfoQuery);
            if (dt.Rows.Count == 0) return false;                    // Не найдено или не «Ожидает»

            int loanId = Convert.ToInt32(dt.Rows[0]["LoanId"]);      // ID займа
            int bookId = Convert.ToInt32(dt.Rows[0]["BookId"]);      // ID книги

            // UPDATE Loans SET Status = 'Отменена' — меняем статус займа
            string query = $@"UPDATE Loans SET Status = N'Отменена', ReturnDate = GETDATE() 
                             WHERE LoanId = {loanId} AND Status = N'Забронирована'";
            int rows = DatabaseHelper.ExecuteNonQuery(query);

            if (rows > 0)                                            // Если обновили займ
            {
                DatabaseHelper.ExecuteNonQuery($"DELETE FROM BookPickups WHERE PickupId = {pickupId}"); // Удаляем запись выдачи
                DatabaseHelper.ExecuteNonQuery($"UPDATE Books SET AvailableCopies = AvailableCopies + 1, IsAvailable = 1 WHERE BookId = {bookId}"); // Возвращаем копию
            }
            return rows > 0;
        }

        public BookPickup? GetPickupByCode(string code) // Найти бронирование по коду (для проверки библиотекарем)
        {
            // bp.* — все колонки BookPickups
            // l.LoanDate — дата займа (JOIN Loans)
            // b.Title as BookTitle — название книги (JOIN Books)
            // u.Username as UserName — логин читателя (JOIN Users)
            // WHERE bp.ReservationCode = '{code}' — ищем точное совпадение кода
            string query = $@"SELECT bp.*, l.LoanDate, b.Title as BookTitle, u.Username as UserName
                             FROM BookPickups bp 
                             INNER JOIN Loans l ON bp.LoanId = l.LoanId 
                             INNER JOIN Books b ON bp.BookId = b.BookId 
                             INNER JOIN Users u ON bp.UserId = u.UserId 
                             WHERE bp.ReservationCode = '{code}'";
            var dt = DatabaseHelper.ExecuteQuery(query);
            if (dt.Rows.Count > 0)                                   // Нашли запись
            {
                var row = dt.Rows[0];
                return new BookPickup
                {
                    PickupId = Convert.ToInt32(row["PickupId"]),
                    LoanId = Convert.ToInt32(row["LoanId"]),
                    BookId = Convert.ToInt32(row["BookId"]),
                    UserId = Convert.ToInt32(row["UserId"]),
                    BookTitle = row["BookTitle"]?.ToString() ?? "",
                    UserName = row["UserName"]?.ToString() ?? "",
                    ReservationCode = row["ReservationCode"]?.ToString() ?? "",
                    LoanDate = Convert.ToDateTime(row["LoanDate"]),
                    Status = row["Status"]?.ToString() ?? "Ожидает"
                };
            }
            return null;                                             // Код не найден
        }


        // ЛОГИ ДЕЙСТВИЙ


        public void LogAction(int adminId, string action, string details) // Записать действие в лог
        {
            // INSERT INTO AdminLogs (AdminId, Action, Details) VALUES (...) — добавляем запись
            // AdminId — кто сделал, Action — что сделал, Details — над чем
            string query = $@"INSERT INTO AdminLogs (AdminId, Action, Details) 
                             VALUES ({adminId}, N'{action.Replace("'", "''")}', N'{details.Replace("'", "''")}')";
            DatabaseHelper.ExecuteNonQuery(query);
        }
    }
}