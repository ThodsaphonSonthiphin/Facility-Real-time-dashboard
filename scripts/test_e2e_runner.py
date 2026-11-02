"""Safety checks for the destructive boundary; no MySQL server is contacted."""
import json
from pathlib import Path
import socket
import tempfile
import unittest
from unittest.mock import patch

import e2e


class CleanupSafetyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.runs = Path(self.temp.name)
        self.directory = self.runs / 'run'
        self.directory.mkdir()
        self.state = {'database': 'facility_e2e_20261031120000_0123abcd',
                      'user': 'e2e_0123abcd', 'token': 'a' * 48, 'cleaned': False, 'userCreated': True}
        self.manifest = self.directory / 'ownership.json'
        self.save()
        self.patcher = patch.object(e2e, 'RUNS', self.runs)
        self.patcher.start()
        self.addCleanup(self.patcher.stop)

    def save(self):
        self.manifest.write_text(json.dumps(self.state))

    @patch.object(e2e, 'sql')
    def test_production_database_is_refused_before_sql(self, sql):
        self.state['database'] = 'facility_dashboard'
        self.save()
        with self.assertRaises(ValueError):
            e2e.clean_run(self.directory)
        sql.assert_not_called()

    @patch.object(e2e, 'sql')
    def test_legacy_fixed_test_database_is_not_adopted(self, sql):
        self.state['database'] = 'facility_e2e_test'
        self.save()
        with self.assertRaises(ValueError):
            e2e.clean_run(self.directory)
        sql.assert_not_called()

    @patch.object(e2e, 'sql')
    def test_user_must_match_database_suffix(self, sql):
        self.state['user'] = 'root'
        self.save()
        with self.assertRaises(ValueError):
            e2e.clean_run(self.directory)
        sql.assert_not_called()

    @patch.object(e2e, 'sql', return_value='another-owner')
    def test_mismatched_owner_cannot_drop(self, sql):
        with self.assertRaises(ValueError):
            e2e.clean_run(self.directory)
        self.assertEqual(sql.call_count, 1)
        self.assertNotIn('DROP', sql.call_args.args[0])

    @patch.object(e2e, 'sql')
    def test_external_manifest_is_refused(self, sql):
        with self.assertRaises(ValueError):
            e2e.clean_run(self.runs)
        sql.assert_not_called()

    @patch.object(e2e, 'sql')
    def test_missing_manifest_cannot_drop(self, sql):
        self.manifest.unlink()
        with self.assertRaises(FileNotFoundError):
            e2e.clean_run(self.directory)
        sql.assert_not_called()

    @patch.object(e2e, 'sql')
    def test_success_only_drops_matching_database_and_user_once(self, sql):
        sql.side_effect = ['a' * 48, '', '']
        e2e.clean_run(self.directory)
        self.assertEqual(sql.call_args_list[1].args[0], "DROP USER IF EXISTS 'e2e_0123abcd'@'localhost';")
        self.assertEqual(sql.call_args_list[2].args[0], "DROP DATABASE `facility_e2e_20261031120000_0123abcd`;")
        self.assertTrue(json.loads(self.manifest.read_text())['cleaned'])
        e2e.clean_run(self.directory)
        self.assertEqual(sql.call_count, 3)

    @patch.object(e2e, 'sql')
    def test_failed_create_user_never_drops_preexisting_account(self, sql):
        self.state['userCreated'] = False
        self.save()
        sql.side_effect = ['a' * 48, '']
        e2e.clean_run(self.directory)
        self.assertEqual(sql.call_count, 2)
        self.assertNotIn('DROP USER', sql.call_args.args[0])

    def retry_after_drop_failure(self, target):
        database_exists, failed = True, False

        def server(statement):
            nonlocal database_exists, failed
            if statement.startswith('SELECT'):
                if not database_exists:
                    raise RuntimeError('Ownership marker was destroyed')
                return self.state['token']
            if statement.startswith(target) and not failed:
                failed = True
                raise RuntimeError('Injected DROP failure')
            if statement.startswith('DROP DATABASE'):
                database_exists = False
            return ''

        with patch.object(e2e, 'sql', side_effect=server):
            with self.assertRaisesRegex(RuntimeError, 'Injected DROP failure'):
                e2e.clean_run(self.directory)
            self.assertTrue(database_exists, 'Keep ownership proof available for retry')
            self.assertFalse(json.loads(self.manifest.read_text())['cleaned'])
            e2e.clean_run(self.directory)
            self.assertFalse(database_exists)
            self.assertTrue(json.loads(self.manifest.read_text())['cleaned'])

    def test_retry_after_user_drop_failure(self):
        self.retry_after_drop_failure('DROP USER')

    def test_retry_after_database_drop_failure(self):
        self.retry_after_drop_failure('DROP DATABASE')


if __name__ == '__main__':
    unittest.main()


class PortProbeTests(unittest.TestCase):
    def test_listening_port_is_refused(self):
        with socket.socket() as other:
            other.bind(('127.0.0.1', 0))
            other.listen()
            with self.assertRaises(OSError):
                e2e.ensure_port_free(other.getsockname()[1])

    def test_previous_runs_time_wait_is_allowed(self):
        with socket.socket() as server:
            server.bind(('127.0.0.1', 0))
            server.listen()
            port = server.getsockname()[1]
            client = socket.create_connection(('127.0.0.1', port))
            accepted, _ = server.accept()
            accepted.close()  # the server side closes first, so the server port is left in TIME_WAIT
            client.close()
        e2e.ensure_port_free(port)
