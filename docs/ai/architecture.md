# 🏗️ Architecture Guidelines

## 🧠 Architecture Style

- Vertical Slice Architecture
- Inspired by Clean Architecture (pragmatic, not strict)

---

## ⚙️ Backend Stack

- .NET 9
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- MediatR

---

## 🎯 Architectural Decisions

### CQRS

- Use ONLY for complex operations
- Do NOT use for simple queries

### Data Access

- Use DbContext directly
- Do NOT implement repository pattern

---

## 📦 Project Structure

Features/
└── {Feature}/
    ├── Command/
    ├── Query/
    ├── Handler/
    └── Validator/

---

## 🔐 Security

- JWT Authentication
- Role-based authorization

---

## 🧪 Testing

- Unit tests with xUnit
- Integration tests for endpoints

---

## ⚠️ Constraints

- Do NOT introduce unnecessary layers
- Do NOT overengineer solutions
- Prefer simplicity over abstraction