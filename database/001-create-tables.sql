-- ============================================================
-- LucyAPI Database Tables
-- Database: lucyapi (on Lito)
-- Created: February 6, 2026
-- ============================================================

-- Users
CREATE TABLE users (
    user_id SERIAL PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Agents (each belongs to one user, carries its own API key)
CREATE TABLE agents (
    agent_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(user_id),
    name TEXT NOT NULL UNIQUE,
    api_key TEXT NOT NULL UNIQUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_agents_user_id ON agents(user_id);
CREATE INDEX idx_agents_api_key ON agents(api_key);

-- Always-Load Context (agent-scoped, tree structure)
-- Core identity and behavioral context that loads every session.
-- Any agent belonging to the same user may read; only the named agent may write.
CREATE TABLE always_load (
    pkid SERIAL PRIMARY KEY,
    agent_id INT NOT NULL REFERENCES agents(agent_id),
    parent_id INT NOT NULL DEFAULT 0,
    title TEXT NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_always_load_agent_id ON always_load(agent_id);
CREATE INDEX idx_always_load_parent_id ON always_load(agent_id, parent_id);

-- Preferences (agent-scoped, tree structure)
-- Domain-specific preferences loaded on demand.
-- Any agent belonging to the same user may read; only the named agent may write.
CREATE TABLE preferences (
    pkid SERIAL PRIMARY KEY,
    agent_id INT NOT NULL REFERENCES agents(agent_id),
    parent_id INT NOT NULL DEFAULT 0,
    title TEXT NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_preferences_agent_id ON preferences(agent_id);
CREATE INDEX idx_preferences_parent_id ON preferences(agent_id, parent_id);

-- Memories (agent-scoped, flat structure)
-- Personal details, stories, relationships. Titles always-load for ambient recall.
-- Agent may add freely. User must approve changes and deletions.
CREATE TABLE memories (
    pkid SERIAL PRIMARY KEY,
    agent_id INT NOT NULL REFERENCES agents(agent_id),
    title TEXT NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_memories_agent_id ON memories(agent_id);

-- Projects (user-scoped, shared across all user's agents)
CREATE TABLE projects (
    project_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(user_id),
    title TEXT NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_projects_user_id ON projects(user_id);

-- Project Sections (tree structure under projects)
CREATE TABLE project_sections (
    section_id SERIAL PRIMARY KEY,
    project_id INT NOT NULL REFERENCES projects(project_id),
    parent_id INT NOT NULL DEFAULT 0,
    title TEXT NOT NULL,
    description TEXT,
    file_path TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_project_sections_project_id ON project_sections(project_id);
CREATE INDEX idx_project_sections_parent_id ON project_sections(project_id, parent_id);

-- Sessions (agent-scoped, tracks conversation starts for gap detection)
CREATE TABLE sessions (
    session_id SERIAL PRIMARY KEY,
    agent_id INT NOT NULL REFERENCES agents(agent_id),
    started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    project TEXT
);

CREATE INDEX idx_sessions_agent_id ON sessions(agent_id, started_at DESC);
