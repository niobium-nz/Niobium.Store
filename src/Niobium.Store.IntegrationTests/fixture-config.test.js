import { test } from 'node:test';
import assert from 'node:assert/strict';
import { approvedRecipient } from './fixture-config.js';

test('fixture rejects production and missing environment before using a recipient', () => {
    for (const TEST_ENVIRONMENT of [undefined, '', 'prod', 'production']) {
        assert.throws(() => approvedRecipient({ TEST_ENVIRONMENT, TEST_NOTIFICATION_RECIPIENT: 'fixture@example.invalid' }), /test_environment_required/);
    }
});

test('fixture requires one explicitly configured recipient without logging its value', () => {
    for (const recipient of ['', 'invalid', 'a@example.invalid,b@example.invalid', ' a@example.invalid', 'Name <a@example.invalid>']) {
        assert.throws(() => approvedRecipient({ TEST_ENVIRONMENT: 'test', TEST_NOTIFICATION_RECIPIENT: recipient }), /^Error: approved_test_recipient_required$/);
    }
    assert.throws(() => approvedRecipient({ TEST_ENVIRONMENT: 'test', EMAIL: 'legacy@example.invalid' }), /approved_test_recipient_required/);
    assert.equal(approvedRecipient({ TEST_ENVIRONMENT: 'test', TEST_NOTIFICATION_RECIPIENT: 'fixture@example.invalid' }), 'fixture@example.invalid');
});
