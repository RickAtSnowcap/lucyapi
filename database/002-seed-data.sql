-- ============================================================
-- LucyAPI Seed Data
-- Initial users, agents, and API keys
-- ============================================================

-- Users
INSERT INTO users (name) VALUES ('alice');
INSERT INTO users (name) VALUES ('bob');

-- Agents (API keys are placeholder — replace before deployment)
INSERT INTO agents (user_id, name, api_key) VALUES
    ((SELECT user_id FROM users WHERE name = 'alice'), 'assistant1', 'REPLACE_WITH_KEY_1'),
    ((SELECT user_id FROM users WHERE name = 'alice'), 'assistant2', 'REPLACE_WITH_KEY_2');

-- Example memory
INSERT INTO memories (agent_id, title, description) VALUES
    ((SELECT agent_id FROM agents WHERE name = 'assistant1'),
     'Sample memory — replace with real data after deployment',
     'This is a placeholder memory demonstrating the schema. Replace seed data with actual values during deployment.');
