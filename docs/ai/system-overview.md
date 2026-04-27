# 🏨 System Overview — Hotel Management Platform

## 📌 Purpose
This system is a hotel management platform designed to handle reservations, pricing, and analytics.

It also integrates AI capabilities for:
- Pricing recommendations
- Occupancy analysis
- Natural language queries

---

## 🎯 Core Features

- Manage guests
- Manage rooms and room types
- Create and manage reservations
- Handle payments
- Dynamic pricing rules
- Reporting and analytics

---

## 🧠 AI Capabilities

- AI assistant to query system data (e.g., "How many reservations this week?")
- Pricing recommendation engine
- Data summarization

---

## 🧩 Core Domain Entities

- Guest
- Room
- RoomType
- Reservation
- Payment
- PricingRule
- OccupancySnapshot

---

## 📊 Business Rules (high level)

- A reservation must have valid check-in and check-out dates
- A room cannot be double-booked
- Total price is calculated at booking time and stored
- Pricing may vary per date

---

## 🚀 Goal of the Project

This project is designed to demonstrate:
- Senior-level .NET architecture
- Clean and maintainable code
- Proper use of CQRS (when needed)
- Secure and scalable backend
- AI-assisted development workflow