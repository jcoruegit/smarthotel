# ⚙️ Engineering Rules for AI Agents

## 📌 General Behavior

- Always generate production-ready code
- Follow existing project structure
- Do not invent new patterns

---

## 🧠 Coding Principles

- Follow SOLID principles
- Prefer simple solutions
- Avoid premature optimization

---

## 🧩 Backend Rules

- Use async/await
- Keep handlers small and focused
- Do not create generic services
- Do not wrap EF Core in repositories

---

## 🎯 CQRS Rules

- Use CQRS only when business logic is complex
- Avoid CQRS for simple reads

---

## 📛 Naming Conventions

- Commands: CreateReservationCommand
- Queries: GetReservationsQuery
- Handlers: {Name}Handler

---

## 🚫 Forbidden Practices

- God classes
- Business logic in controllers
- Overuse of abstractions
- Duplicate logic

---

## 🤖 AI-Specific Rules

When generating code:

1. Do NOT assume missing requirements
2. Ask for clarification if needed
3. Prefer minimal viable implementation
4. Follow existing patterns in the repository