"""Safety checks for the destructive boundary; no MySQL server is contacted."""
import json
from pathlib import Path
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
        self.assertEqual(sql.call_args_list[1].args[0], "DROP DATABASE `facility_e2e_20261031120000_0123abcd`;")
        self.assertEqual(sql.call_args_list[2].args[0], "DROP USER IF EXISTS 'e2e_0123abcd'@'localhost';")
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


if __name__ == '__main__':
    unittest.main()
