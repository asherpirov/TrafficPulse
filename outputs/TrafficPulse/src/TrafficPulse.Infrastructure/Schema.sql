CREATE TABLE IF NOT EXISTS users (
 id CHAR(36) PRIMARY KEY, email VARCHAR(254) NOT NULL UNIQUE, name VARCHAR(80) NOT NULL,
 password_hash VARCHAR(512) NOT NULL, role VARCHAR(16) NOT NULL, enabled BOOLEAN NOT NULL
);
CREATE TABLE IF NOT EXISTS sessions (
 token_hash CHAR(64) PRIMARY KEY, user_id CHAR(36) NOT NULL, csrf_token VARCHAR(64) NOT NULL,
 expires_at DATETIME(6) NOT NULL, INDEX(expires_at), FOREIGN KEY(user_id) REFERENCES users(id)
);
CREATE TABLE IF NOT EXISTS segments (
 id CHAR(36) PRIMARY KEY, name VARCHAR(80) NOT NULL, latitude DOUBLE NOT NULL, longitude DOUBLE NOT NULL,
 enabled BOOLEAN NOT NULL, version INT NOT NULL
);
CREATE TABLE IF NOT EXISTS readings (
 id CHAR(36) PRIMARY KEY, segment_id CHAR(36) NOT NULL, observed_at DATETIME(6) NOT NULL,
 speed DOUBLE NOT NULL, free_flow_speed DOUBLE NOT NULL, confidence DOUBLE NOT NULL,
 road_closed BOOLEAN NOT NULL, source VARCHAR(16) NOT NULL,
 INDEX ix_readings_segment_time(segment_id, observed_at), INDEX ix_readings_time(observed_at),
 FOREIGN KEY(segment_id) REFERENCES segments(id)
);
CREATE TABLE IF NOT EXISTS anomalies (
 id CHAR(36) PRIMARY KEY, reading_id CHAR(36) NOT NULL UNIQUE, segment_id CHAR(36) NOT NULL,
 created_at DATETIME(6) NOT NULL, reason VARCHAR(500) NOT NULL, average DOUBLE NULL, deviation DOUBLE NULL,
 samples INT NOT NULL, status VARCHAR(20) NOT NULL, version INT NOT NULL,
 INDEX ix_anomalies_status_time(status, created_at), FOREIGN KEY(reading_id) REFERENCES readings(id),
 FOREIGN KEY(segment_id) REFERENCES segments(id)
);
CREATE TABLE IF NOT EXISTS favorites (
 user_id CHAR(36) NOT NULL, segment_id CHAR(36) NOT NULL, PRIMARY KEY(user_id,segment_id),
 FOREIGN KEY(user_id) REFERENCES users(id), FOREIGN KEY(segment_id) REFERENCES segments(id)
);
CREATE TABLE IF NOT EXISTS audit_log (
 id CHAR(36) PRIMARY KEY, actor_id CHAR(36) NOT NULL, action VARCHAR(80) NOT NULL,
 target VARCHAR(80) NOT NULL, at DATETIME(6) NOT NULL, INDEX(at), FOREIGN KEY(actor_id) REFERENCES users(id)
);
CREATE TABLE IF NOT EXISTS schema_versions (version INT PRIMARY KEY, applied_at DATETIME(6) NOT NULL);
INSERT IGNORE INTO schema_versions VALUES (1, UTC_TIMESTAMP(6));
