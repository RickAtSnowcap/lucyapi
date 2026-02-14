-- ============================================================
-- LucyAPI Seed Data
-- Initial users, agents, and API keys
-- ============================================================

-- Users
INSERT INTO users (name) VALUES ('rick');
INSERT INTO users (name) VALUES ('pam');

-- Agents (API keys are placeholder — replace before deployment)
INSERT INTO agents (user_id, name, api_key) VALUES
    ((SELECT user_id FROM users WHERE name = 'rick'), 'lucy', 'REPLACE_WITH_LUCY_KEY'),
    ((SELECT user_id FROM users WHERE name = 'rick'), 'linus', 'REPLACE_WITH_LINUS_KEY');

-- Rick's first memory (approved in conversation Feb 6, 2026)
INSERT INTO memories (agent_id, title, description) VALUES
    ((SELECT agent_id FROM agents WHERE name = 'lucy'),
     'DVT/PE survivor — hourly movement breaks non-negotiable since mid-twenties',
     'Rick suffered a Deep Vein Thrombosis in his mid-twenties from sitting too long in a bad office chair while building his first startup. The DVT broke clots loose and caused two hospitalizations for Pulmonary Embolism. Doctors told him if he didn''t get up and move regularly, he''d die in his thirties. He''s 58 and still disciplined about hourly breaks — no reminders needed. Future: once agents have temporal awareness, Rick will grant permission to remind him if he ever forgets, but it''s unlikely.');
