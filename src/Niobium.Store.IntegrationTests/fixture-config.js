import { pathToFileURL } from 'node:url';

export function approvedRecipient(environment = process.env) {
    if (environment.TEST_ENVIRONMENT !== 'test') {
        throw new Error('test_environment_required');
    }
    const recipient = environment.TEST_NOTIFICATION_RECIPIENT || '';
    if (!/^[^\s,;<>@]+@[^\s,;<>@]+\.[^\s,;<>@]+$/.test(recipient)) {
        throw new Error('approved_test_recipient_required');
    }
    return recipient;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    try {
        approvedRecipient();
        console.log('PASS test_fixture_configuration');
    } catch (error) {
        console.error(`FAIL ${error.message}`);
        process.exitCode = 1;
    }
}
