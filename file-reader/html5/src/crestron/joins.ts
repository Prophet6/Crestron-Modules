import type { AnalogJoin, DigitalJoin, SerialJoin } from './types';
import { FORMATS, SLOT_COUNT } from '../signals';

function b(name: string): DigitalJoin {
  return { type: 'b', name };
}

function n(name: string): AnalogJoin {
  return { type: 'n', name };
}

function s(name: string): SerialJoin {
  return { type: 's', name };
}

/**
 * Name-based CH5 contract.
 * Signal names match contracts/file-reader.cse2j.
 */
export const Joins = {
  read: b('FileReader.Read'),
  write: b('FileReader.Write'),
  delete: b('FileReader.Delete'),
  exists: b('FileReader.Exists_FB'),
  notExist: b('FileReader.Not_Exist_FB'),
  busy: b('FileReader.Busy_FB'),
  encrypt: b('FileReader.Encrypt'),
  encrypted: b('FileReader.Encrypted_FB'),
  enableBackup: b('FileReader.Enable_Backup'),
  enableBackupHistory: b('FileReader.Enable_Backup_History'),
  restoreBackup: b('FileReader.Restore_Backup'),
  deleteBackup: b('FileReader.Delete_Backup'),
  backupExists: b('FileReader.Backup_Exists_FB'),
  autoCreate: b('FileReader.Auto_Create_File'),
  setDefaultValues: b('FileReader.Set_Default_Values'),
  matchPartial: b('FileReader.Match_Partial'),
  fileLocationOverride: n('FileReader.File_Location_Override'),
  formatAnalog: n('FileReader.Format'),
  detectedFormat: n('FileReader.Detected_Format_FB'),
  fileNameOverride: s('FileReader.FileName_Override'),
  fileNameFb: s('FileReader.FileName_FB'),
  feedback: s('FileReader.Operational_Feedback'),
  size: s('FileReader.Size_FB'),
  encryptionKey: s('FileReader.Encryption_Key'),
} as const;

export const formatJoins = FORMATS.map((item) => ({
  code: item.code,
  name: item.name,
  press: b(`FileReader.Format_${item.name}`),
  feedback: b(`FileReader.Format_${item.name}_FB`),
}));

export interface SlotJoins {
  digitalIn: DigitalJoin;
  digitalOut: DigitalJoin;
  analogIn: AnalogJoin;
  analogOut: AnalogJoin;
  serialIn: SerialJoin;
  serialOut: SerialJoin;
}

export const slotJoins: SlotJoins[] = Array.from({ length: SLOT_COUNT }, (_, index) => {
  const slot = index + 1;
  return {
    digitalIn: b(`FileReader.Digital_Input_${slot}`),
    digitalOut: b(`FileReader.Digital_Output_${slot}`),
    analogIn: n(`FileReader.Analog_Input_${slot}`),
    analogOut: n(`FileReader.Analog_Output_${slot}`),
    serialIn: s(`FileReader.Serial_Input_${slot}`),
    serialOut: s(`FileReader.Serial_Output_${slot}`),
  };
});
