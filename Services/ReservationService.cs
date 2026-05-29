using System.Data;                             // DataTable, DataRow — работа с результатами SQL-запросов
using LibraryApp.Database;                     // DatabaseHelper — выполнение запросов к базе данных
using LibraryApp.Models;                       // Reservation — модель данных бронирования

namespace LibraryApp.Services
{
    public class ReservationService            // Сервис для работы с бронированиями книг (Reservations)
    {
        private static readonly Random _random = new Random(); // static — один генератор на всех; readonly — неизменяемый


        // ГЕНЕРАЦИЯ СЛУЧАЙНОГО 8-ЗНАЧНОГО КОДА

        public string GenerateReservationCode()  // Возвращает строку из 8 случайных символов
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Без 0/O/1/I чтобы не путать
            char[] code = new char[8];           // Массив на 8 символов — будущий код
            for (int i = 0; i < code.Length; i++) // Заполняем каждый символ
            {
                code[i] = chars[_random.Next(chars.Length)]; // Случайный индекс в строке chars
            }
            return new string(code);             // Преобразуем char[] → string
        }


        // СОЗДАНИЕ БРОНИРОВАНИЯ (ВЫЗЫВАЕТСЯ ПОЛЬЗОВАТЕЛЕМ)

        public string? ReserveBook(int userId, int bookId) // ? — может вернуть null; userId — кто бронирует, bookId — какую книгу
        {
            string checkQuery = $"SELECT IsAvailable FROM Books WHERE BookId = {bookId}"; // Проверка доступности книги
            var result = DatabaseHelper.ExecuteScalar(checkQuery); // Одно значение: true/false или null

            if (result == null || !Convert.ToBoolean(result)) // Книга не найдена или недоступна
                return null;                                 // null — не удалось забронировать

            string checkReservation = $@"SELECT COUNT(*) FROM Reservations 
                                        WHERE UserId = {userId} AND BookId = {bookId} 
                                        AND Status = N'Активно' AND ExpiryDate > GETDATE()"; // Уже есть активное бронирование?
            var existingReservation = DatabaseHelper.ExecuteScalar(checkReservation);

            if (existingReservation != null && Convert.ToInt32(existingReservation) > 0) // Уже забронирована
                return "ALREADY_RESERVED";                   // Специальный код: уже есть бронь

            string code = GenerateReservationCode();         // Генерируем уникальный код

            while (Convert.ToInt32(DatabaseHelper.ExecuteScalar(                 // Пока код уже существует в БД
                $"SELECT COUNT(*) FROM Reservations WHERE ReservationCode = '{code}'")) > 0)
            {
                code = GenerateReservationCode();            // Генерируем новый код
            }

            DateTime expiryDate = DateTime.Now.AddDays(3);   // Срок действия брони — 3 дня

            string query = $@"INSERT INTO Reservations (ReservationCode, UserId, BookId, ExpiryDate) 
                             VALUES ('{code}', {userId}, {bookId}, '{expiryDate:yyyy-MM-dd HH:mm:ss}')"; // Создаём запись
            DatabaseHelper.ExecuteNonQuery(query);

            query = $"UPDATE Books SET IsAvailable = 0 WHERE BookId = {bookId}"; // Делаем книгу недоступной для других
            DatabaseHelper.ExecuteNonQuery(query);

            return code;                                     // Возвращаем код бронирования пользователю
        }


        // ОТМЕНА БРОНИРОВАНИЯ ПОЛЬЗОВАТЕЛЕМ

        public bool CancelReservation(int userId, string reservationCode) // true — успешно, false — не найдено
        {
            string query = $@"UPDATE Reservations SET Status = N'Отменено' 
                             WHERE ReservationCode = '{reservationCode}' 
                             AND UserId = {userId} AND Status = N'Активно'"; // Только свои и только активные

            int rowsAffected = DatabaseHelper.ExecuteNonQuery(query); // Сколько строк обновлено (0 или 1)

            if (rowsAffected > 0)                            // Нашли и отменили
            {
                string freeBookQuery = $@"UPDATE Books SET IsAvailable = 1 
                                         WHERE BookId = (SELECT BookId FROM Reservations WHERE ReservationCode = '{reservationCode}')"; // Освобождаем книгу
                DatabaseHelper.ExecuteNonQuery(freeBookQuery);
                return true;
            }
            return false;                                    // Не нашли или не тот статус
        }


        // ПОДТВЕРЖДЕНИЕ БРОНИРОВАНИЯ БИБЛИОТЕКАРЕМ (ВЫДАЧА КНИГИ)

        public bool ConfirmReservation(string reservationCode, int librarianId) // librarianId — кто выдал
        {
            string checkQuery = $@"SELECT ReservationId, UserId, BookId, Status, ExpiryDate 
                                   FROM Reservations 
                                   WHERE ReservationCode = '{reservationCode}'"; // Ищем бронь по коду

            var dt = DatabaseHelper.ExecuteQuery(checkQuery);

            if (dt.Rows.Count == 0) return false;            // Код не найден

            var row = dt.Rows[0];
            string status = row["Status"]?.ToString() ?? ""; // Текущий статус
            DateTime expiryDate = Convert.ToDateTime(row["ExpiryDate"]); // Срок действия

            if (status != "Активно" || DateTime.Now > expiryDate) // Просрочено или не активно
            {
                if (status == "Активно" && DateTime.Now > expiryDate) // Автоматически помечаем просроченным
                {
                    DatabaseHelper.ExecuteNonQuery($"UPDATE Reservations SET Status = N'Просрочено' WHERE ReservationCode = '{reservationCode}'");
                }
                return false;
            }

            int userId = Convert.ToInt32(row["UserId"]);     // Кому выдаём
            int bookId = Convert.ToInt32(row["BookId"]);     // Какую книгу

            string loanQuery = $@"INSERT INTO Loans (UserId, BookId, DueDate, Status) 
                                 VALUES ({userId}, {bookId}, '{DateTime.Now.AddDays(14):yyyy-MM-dd}', N'Выдана')"; // Создаём займ на 14 дней
            DatabaseHelper.ExecuteNonQuery(loanQuery);

            string updateReservationQuery = $@"UPDATE Reservations SET Status = N'Выдано', LibrarianId = {librarianId} 
                                              WHERE ReservationCode = '{reservationCode}'"; // Обновляем статус брони
            DatabaseHelper.ExecuteNonQuery(updateReservationQuery);

            return true;
        }


        // ПОЛУЧЕНИЕ ИНФОРМАЦИИ О БРОНИРОВАНИИ ПО КОДУ

        public Reservation? GetReservationByCode(string code) // ? — может вернуть null если не найден
                                                              // INNER JOIN Users u ON r.UserId = u.UserId - JOIN: логин читателя
                                                              // INNER JOIN Books b ON r.BookId = b.BookId - JOIN: название книги
                                                              // INNER JOIN Authors a ON b.AuthorId = a.AuthorId - JOIN: имя автора
                                                              // LEFT JOIN Users l ON r.LibrarianId = l.UserId - LEFT JOIN: имя библиотекаря (может быть NULL)
        {
            string query = $@"SELECT r.*, u.Username, b.Title as BookTitle, a.AuthorName,
                             l.Username as LibrarianName
                             FROM Reservations r 
                             INNER JOIN Users u ON r.UserId = u.UserId           
                             INNER JOIN Books b ON r.BookId = b.BookId           
                             INNER JOIN Authors a ON b.AuthorId = a.AuthorId     
                             LEFT JOIN Users l ON r.LibrarianId = l.UserId       
                             WHERE r.ReservationCode = '{code}'";

            var dt = DatabaseHelper.ExecuteQuery(query);

            if (dt.Rows.Count == 0) return null;             // Не найдено

            var row = dt.Rows[0];
            return new Reservation                            // Создаём объект Reservation из строки
            {
                ReservationId = Convert.ToInt32(row["ReservationId"]),
                ReservationCode = row["ReservationCode"]?.ToString() ?? "",
                UserId = Convert.ToInt32(row["UserId"]),
                Username = row["Username"]?.ToString() ?? "",   // Из JOIN с Users
                BookId = Convert.ToInt32(row["BookId"]),
                BookTitle = row["BookTitle"]?.ToString() ?? "", // Из JOIN с Books
                AuthorName = row["AuthorName"]?.ToString() ?? "", // Из JOIN с Authors
                ReservationDate = Convert.ToDateTime(row["ReservationDate"]),
                ExpiryDate = Convert.ToDateTime(row["ExpiryDate"]),
                Status = row["Status"]?.ToString() ?? "",
                LibrarianId = row["LibrarianId"] != DBNull.Value ? Convert.ToInt32(row["LibrarianId"]) : null, // Может быть NULL
                LibrarianName = row["LibrarianName"]?.ToString() ?? "" // Из LEFT JOIN с Users
            };
        }


        // ПОЛУЧЕНИЕ АКТИВНЫХ БРОНИРОВАНИЙ ПОЛЬЗОВАТЕЛЯ

        public List<Reservation> GetUserReservations(int userId) // Все бронирования пользователя
        {
            var reservations = new List<Reservation>();
            string query = $@"SELECT r.*, u.Username, b.Title as BookTitle, a.AuthorName
                             FROM Reservations r 
                             INNER JOIN Users u ON r.UserId = u.UserId 
                             INNER JOIN Books b ON r.BookId = b.BookId 
                             INNER JOIN Authors a ON b.AuthorId = a.AuthorId 
                             WHERE r.UserId = {userId} 
                             ORDER BY r.ReservationDate DESC";  // Новые сверху

            var dt = DatabaseHelper.ExecuteQuery(query);

            foreach (DataRow row in dt.Rows)                  // Перебираем строки результата
            {
                reservations.Add(new Reservation
                {
                    ReservationId = Convert.ToInt32(row["ReservationId"]),
                    ReservationCode = row["ReservationCode"]?.ToString() ?? "",
                    UserId = Convert.ToInt32(row["UserId"]),
                    Username = row["Username"]?.ToString() ?? "",
                    BookId = Convert.ToInt32(row["BookId"]),
                    BookTitle = row["BookTitle"]?.ToString() ?? "",
                    AuthorName = row["AuthorName"]?.ToString() ?? "",
                    ReservationDate = Convert.ToDateTime(row["ReservationDate"]),
                    ExpiryDate = Convert.ToDateTime(row["ExpiryDate"]),
                    Status = row["Status"]?.ToString() ?? ""
                });
            }
            return reservations;
        }


        // ПОЛУЧЕНИЕ ВСЕХ АКТИВНЫХ БРОНИРОВАНИЙ (ДЛЯ АДМИНА)

        public DataTable GetAllActiveReservations()           // Возвращает DataTable для отображения в таблице
                                                              // WHERE r.Status = N'Активно'
        {
            return DatabaseHelper.ExecuteQuery(
                @"SELECT r.ReservationCode, r.ReservationDate, r.ExpiryDate, r.Status,
                  b.Title, a.AuthorName, u.Username, u.FullName
                  FROM Reservations r 
                  INNER JOIN Books b ON r.BookId = b.BookId 
                  INNER JOIN Authors a ON b.AuthorId = a.AuthorId 
                  INNER JOIN Users u ON r.UserId = u.UserId 
                  WHERE r.Status = N'Активно'                
                  ORDER BY r.ReservationDate DESC");
        }


        // ОБНОВЛЕНИЕ ПРОСРОЧЕННЫХ БРОНИРОВАНИЙ

        public void UpdateExpiredReservations()               // Автоматически снимает бронь с просроченных
        {
            string query = @"UPDATE Books SET IsAvailable = 1 
                            WHERE BookId IN (
                                SELECT BookId FROM Reservations 
                                WHERE Status = N'Активно' AND ExpiryDate < GETDATE()
                            )";                               // Освобождаем книги с истекшим сроком
            DatabaseHelper.ExecuteNonQuery(query);

            query = @"UPDATE Reservations SET Status = N'Просрочено' 
                     WHERE Status = N'Активно' AND ExpiryDate < GETDATE()"; // Помечаем брони как просроченные
            DatabaseHelper.ExecuteNonQuery(query);
        }
    }
}