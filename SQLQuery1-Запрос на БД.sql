
-- СОЗДАНИЕ БАЗЫ ДАННЫХ

CREATE DATABASE KursWork; -- Lea - имя БД
GO

USE KursWork;
GO


-- ТАБЛИЦА РОЛЕЙ

CREATE TABLE Roles (
    RoleId INT IDENTITY(1,1) PRIMARY KEY,
    RoleName NVARCHAR(50) NOT NULL UNIQUE
);

INSERT INTO Roles (RoleName) VALUES 
(N'Admin'),
(N'Librarian'),
(N'User');
GO


-- ТАБЛИЦА ПОЛЬЗОВАТЕЛЕЙ

CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    Password NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100),
    FullName NVARCHAR(100),
    RegistrationDate DATETIME DEFAULT GETDATE(),
    IsActive BIT DEFAULT 1,
    RoleId INT DEFAULT 3,
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);
-- Добавляем трех пользователей трех резных ролей
-- Администратор (логин: admin, пароль: admin123)
INSERT INTO Users (Username, Password, Email, FullName, RoleId) VALUES 
('admin', 'admin123', 'admin@library.com', N'Администратор Системы', 1);
-- Библиотекарь (логин: librarian, пароль: lib123)
INSERT INTO Users (Username, Password, Email, FullName, RoleId) VALUES 
('librarian', 'lib123', 'librarian@library.com', N'Библиотекарь Петров', 2);

-- Обычный пользователь (логин: user, пароль: user123)
INSERT INTO Users (Username, Password, Email, FullName, RoleId) VALUES 
('user', 'user123', 'user@mail.com', N'Иван Иванов', 3);
GO


-- ТАБЛИЦА ЖАНРОВ

CREATE TABLE Genres (
    GenreId INT IDENTITY(1,1) PRIMARY KEY,
    GenreName NVARCHAR(50) NOT NULL UNIQUE
);
GO


-- ТАБЛИЦА АВТОРОВ

CREATE TABLE Authors (
    AuthorId INT IDENTITY(1,1) PRIMARY KEY,
    AuthorName NVARCHAR(100) NOT NULL UNIQUE
);
GO


-- ТАБЛИЦА КНИГ

CREATE TABLE Books (
    BookId INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(200) NOT NULL,
    AuthorId INT NOT NULL,
    GenreId INT NOT NULL,
    PublicationYear INT,
    Language NVARCHAR(20) DEFAULT N'Русский',
    Annotation NVARCHAR(MAX),
    CoverImage VARBINARY(MAX),
    PdfContent VARBINARY(MAX),
    BookType NVARCHAR(20) DEFAULT N'Печатная',
    TotalCopies INT DEFAULT 1,
    AvailableCopies INT DEFAULT 1,
    Rating DECIMAL(3,2) DEFAULT 0,
    Popularity INT DEFAULT 0,
    DateAdded DATETIME DEFAULT GETDATE(),
    IsNew BIT DEFAULT 1,
    IsHit BIT DEFAULT 0,
    IsAvailable BIT DEFAULT 1,
    CONSTRAINT FK_Books_Authors FOREIGN KEY (AuthorId) REFERENCES Authors(AuthorId),
    CONSTRAINT FK_Books_Genres FOREIGN KEY (GenreId) REFERENCES Genres(GenreId)
);
GO


-- ТАБЛИЦА ЗАЙМОВ

CREATE TABLE Loans (
    LoanId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    BookId INT NOT NULL,
    LoanDate DATETIME DEFAULT GETDATE(),
    ReturnDate DATETIME,
    DueDate DATETIME,
    Status NVARCHAR(20) DEFAULT N'Выдана',
    IsOnline BIT DEFAULT 0,
    ReservationCode NVARCHAR(20),
    CONSTRAINT FK_Loans_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_Loans_Books FOREIGN KEY (BookId) REFERENCES Books(BookId)
);
GO


-- ТАБЛИЦА ИСТОРИИ ПРОЧИТАННЫХ КНИГ

CREATE TABLE ReadHistory (
    ReadHistoryId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    BookId INT NOT NULL,
    ReadDate DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_ReadHistory_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_ReadHistory_Books FOREIGN KEY (BookId) REFERENCES Books(BookId)
);
GO


-- ТАБЛИЦА ОТЗЫВОВ

CREATE TABLE Reviews (
    ReviewId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    BookId INT NOT NULL,
    Rating INT CHECK (Rating BETWEEN 1 AND 5),
    Comment NVARCHAR(MAX),
    ReviewDate DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_Reviews_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_Reviews_Books FOREIGN KEY (BookId) REFERENCES Books(BookId)
);
GO


-- ТАБЛИЦА ЗАКЛАДОК

CREATE TABLE Bookmarks (
    BookmarkId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    BookId INT NOT NULL,
    DateAdded DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_Bookmarks_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_Bookmarks_Books FOREIGN KEY (BookId) REFERENCES Books(BookId)
);
GO


-- ТАБЛИЦА НОВОСТЕЙ

CREATE TABLE News (
    NewsId INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(200) NOT NULL,
    Content NVARCHAR(MAX),
    PublishDate DATETIME DEFAULT GETDATE(),
    IsActive BIT DEFAULT 1
);
GO


-- ТАБЛИЦА ЛОГОВ АДМИНИСТРАТОРА

CREATE TABLE AdminLogs (
    LogId INT IDENTITY(1,1) PRIMARY KEY,
    AdminId INT NOT NULL,
    Action NVARCHAR(200) NOT NULL,
    Details NVARCHAR(MAX),
    ActionDate DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_AdminLogs_Users FOREIGN KEY (AdminId) REFERENCES Users(UserId)
);
GO


-- ТАБЛИЦА ВЫДАЧИ КНИГ БИБЛИОТЕКАРЕМ

CREATE TABLE BookPickups (
    PickupId INT IDENTITY(1,1) PRIMARY KEY,
    LoanId INT NOT NULL,
    BookId INT NOT NULL,
    UserId INT NOT NULL,
    ReservationCode NVARCHAR(20) NOT NULL,
    PickupDate DATETIME,
    Status NVARCHAR(20) DEFAULT N'Ожидает',
    LibrarianId INT,
    CONSTRAINT FK_BookPickups_Loans FOREIGN KEY (LoanId) REFERENCES Loans(LoanId),
    CONSTRAINT FK_BookPickups_Books FOREIGN KEY (BookId) REFERENCES Books(BookId),
    CONSTRAINT FK_BookPickups_Users FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_BookPickups_Librarian FOREIGN KEY (LibrarianId) REFERENCES Users(UserId)
);
GO


-- ИНДЕКСЫ

CREATE INDEX IX_Users_Username ON Users(Username);
CREATE INDEX IX_Users_RoleId ON Users(RoleId);
CREATE INDEX IX_Books_AuthorId ON Books(AuthorId);
CREATE INDEX IX_Books_GenreId ON Books(GenreId);
CREATE INDEX IX_Loans_UserId ON Loans(UserId);
CREATE INDEX IX_Loans_BookId ON Loans(BookId);
CREATE INDEX IX_Loans_Status ON Loans(Status);
CREATE INDEX IX_Loans_ReservationCode ON Loans(ReservationCode);
CREATE INDEX IX_BookPickups_ReservationCode ON BookPickups(ReservationCode);
CREATE INDEX IX_BookPickups_Status ON BookPickups(Status);
CREATE INDEX IX_ReadHistory_UserId ON ReadHistory(UserId);
CREATE INDEX IX_Bookmarks_UserId ON Bookmarks(UserId);
CREATE INDEX IX_Reviews_BookId ON Reviews(BookId);
CREATE INDEX IX_AdminLogs_ActionDate ON AdminLogs(ActionDate);
GO