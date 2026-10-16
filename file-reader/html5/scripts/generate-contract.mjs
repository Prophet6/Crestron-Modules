#!/usr/bin/env node
/**
 * Builds the File Reader CH5 contract:
 *   contracts/file-reader.cse2j       — CH5 / WebXPanel mapping
 *   public/config/contract.cse2j      — runtime path CrComLib expects
 *   contracts/file-reader.chd         — SIMPL Windows GUI extender
 *   docs/CONTRACT-MAP.md              — row grid for wiring
 *
 * CHD layout matches Construct GUI-extender files (Sgntr=CHD).
 * Input cues = program → panel (states). Output cues = panel → program (events).
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const timestamp = new Date().toISOString().replace('T', ' ').replace('Z', '');
const contractName = 'FileReader';
const contractId = 'file-reader-contracts-v1';
const slotCount = 64;

/** Empty cell. Written as `[~UNUSED~]` in the CHD — the same blank SIMPL+ `_SKIP_` becomes. */
const SKIP = null;

const formatNames = ['Text', 'CSV', 'XML', 'JSON', 'INI', 'Binary'];

function slots(prefix) {
  return Array.from({ length: slotCount }, (_, index) => `${prefix}${index + 1}`);
}

/**
 * Each list is one column of one signal type, top to bottom.
 * `SKIP` pads that column so a signal shares a row with its partner.
 * A `SKIP` on both sides is a blank row between groups.
 *
 * @typedef {{ inputs: Array<string|null>, outputs: Array<string|null> }} Column
 */
const components = [
  {
    instance: 'FileReader',
    smartObjectId: 1,
    hint: 'File Reader v3.3.0. Inputs are module outputs. Outputs are module inputs. Blank rows separate groups.',
    digital: column(
      [
        'Exists_FB',
        'Not_Exist_FB',
        'Busy_FB',
        SKIP,
        'Encrypted_FB',
        'Backup_Exists_FB',
        SKIP,
        SKIP,
        SKIP,
        'Set_Default_Values',
        SKIP,
        SKIP,
        ...formatNames.map((name) => `Format_${name}_FB`),
        SKIP,
        ...slots('Digital_Output_'),
      ],
      [
        'Read',
        'Write',
        'Delete',
        SKIP,
        'Encrypt',
        'Enable_Backup',
        'Enable_Backup_History',
        'Restore_Backup',
        'Delete_Backup',
        'Auto_Create_File',
        'Match_Partial',
        SKIP,
        ...formatNames.map((name) => `Format_${name}`),
        SKIP,
        ...slots('Digital_Input_'),
      ],
    ),
    analog: column(
      [SKIP, 'Detected_Format_FB', SKIP, ...slots('Analog_Output_')],
      ['File_Location_Override', 'Format', SKIP, ...slots('Analog_Input_')],
    ),
    serial: column(
      ['FileName_FB', 'Operational_Feedback', 'Size_FB', SKIP, SKIP, ...slots('Serial_Output_')],
      ['FileName_Override', SKIP, SKIP, 'Encryption_Key', SKIP, ...slots('Serial_Input_')],
    ),
  },
];

function column(inputs, outputs) {
  if (inputs.length !== outputs.length) {
    throw new Error(`Column length ${inputs.length} does not match ${outputs.length}`);
  }
  assertUnique(inputs, 'input');
  assertUnique(outputs, 'output');
  return { inputs, outputs };
}

function assertUnique(list, label) {
  const seen = new Set();
  for (const name of list) {
    if (!name) {
      continue;
    }
    if (seen.has(name)) {
      throw new Error(`Duplicate ${label} ${name}`);
    }
    seen.add(name);
  }
}

const columns = {
  b: 'digital',
  n: 'analog',
  s: 'serial',
};

/** Contract join ids count real signals only. `[~UNUSED~]` is a visual row, not a join. */
function namedJoins(list) {
  const map = {};
  let join = 1;
  for (const name of list) {
    if (!name) {
      continue;
    }
    map[String(join)] = name;
    join += 1;
  }
  return map;
}

function eventEntries(instance, list, smartObjectId) {
  const out = {};
  let join = 1;
  for (const name of list) {
    if (!name) {
      continue;
    }
    out[`${instance}.${name}`] = { joinId: join, smartObjectId };
    join += 1;
  }
  return out;
}

function buildCse2j() {
  const states = { boolean: {}, numeric: {}, string: {} };
  const events = { boolean: {}, numeric: {}, string: {} };
  const typeKey = { b: 'boolean', n: 'numeric', s: 'string' };

  for (const c of components) {
    const id = String(c.smartObjectId);
    for (const t of /** @type {const} */ (['b', 'n', 's'])) {
      const columnPair = c[columns[t]];
      const stateMap = namedJoins(columnPair.inputs);
      if (Object.keys(stateMap).length) {
        const prefixed = {};
        for (const [join, name] of Object.entries(stateMap)) {
          prefixed[join] = `${c.instance}.${name}`;
        }
        states[typeKey[t]][id] = prefixed;
      }
      Object.assign(events[typeKey[t]], eventEntries(c.instance, columnPair.outputs, c.smartObjectId));
    }
  }

  return {
    name: contractName,
    timestamp,
    version: '1.0.0.0',
    schema_version: 1,
    extra_value: 'File Reader v3.3.0 name-based CH5 contract',
    signals: { states, events },
  };
}

function crlf(text) {
  return text.replace(/\r\n/g, '\n').replace(/\n/g, '\r\n');
}

function writeColumn(lines, component, list, spec) {
  list.forEach((name, index) => {
    const n = index + 1;
    lines.push(`${spec.cue}${n}=${name ?? '[~UNUSED~]'}`);
    lines.push(`${spec.sigType}${n}=${spec.typeName}`);
    if (name) {
      lines.push(`${spec.tip}${n}=${component.instance}.${name} (${spec.direction})`);
    }
  });
}

function buildChdSymbol(c) {
  const dIn = c.digital.inputs;
  const dOut = c.digital.outputs;
  const aIn = c.analog.inputs;
  const aOut = c.analog.outputs;
  const sIn = c.serial.inputs;
  const sOut = c.serial.outputs;
  const id = c.smartObjectId;
  const lines = [
    '[',
    'ObjTp=Symbol',
    `Name=${c.instance}`,
    `SmplCName=${contractName}.${c.instance}`,
    `Hint=${c.instance} (Control Join Id ${id}) — ${c.hint}`,
    `Code=${id}`,
    'SMWRev=4.28.00',
    'Expand=expand_separately',
    `MinVariableInputs=${dIn.length}`,
    `MaxVariableInputs=${dIn.length}`,
    `MinVariableOutputs=${dOut.length}`,
    `MaxVariableOutputs=${dOut.length}`,
    `MinVariableInputsList2=${aIn.length}`,
    `MaxVariableInputsList2=${aIn.length}`,
    `MinVariableOutputsList2=${aOut.length}`,
    `MaxVariableOutputsList2=${aOut.length}`,
    `MinVariableInputsList3=${sIn.length}`,
    `MaxVariableInputsList3=${sIn.length}`,
    `MinVariableOutputsList3=${sOut.length}`,
    `MaxVariableOutputsList3=${sOut.length}`,
    'NumFixedParams=1',
    'ParamCue1=ControlJoinId',
    'ParamSigType1=UI_RO_String',
    `ControlJoinId=${id}d`,
    'MPp=1',
    `Pp1=${id}`,
    `ChdH=${id}`,
  ];

  writeColumn(lines, c, dIn, {
    cue: 'InputCue',
    sigType: 'InputSigType',
    typeName: 'Digital',
    tip: 'InputToolTip',
    direction: 'program → panel',
  });
  writeColumn(lines, c, dOut, {
    cue: 'OutputCue',
    sigType: 'OutputSigType',
    typeName: 'Digital',
    tip: 'OutputToolTip',
    direction: 'panel → program',
  });
  writeColumn(lines, c, aIn, {
    cue: 'InputList2Cue',
    sigType: 'InputList2SigType',
    typeName: 'Analog',
    tip: 'Input2ToolTip',
    direction: 'program → panel',
  });
  writeColumn(lines, c, aOut, {
    cue: 'OutputList2Cue',
    sigType: 'OutputList2SigType',
    typeName: 'Analog',
    tip: 'Output2ToolTip',
    direction: 'panel → program',
  });
  writeColumn(lines, c, sIn, {
    cue: 'InputList3Cue',
    sigType: 'InputList3SigType',
    typeName: 'Serial',
    tip: 'Input3ToolTip',
    direction: 'program → panel',
  });
  writeColumn(lines, c, sOut, {
    cue: 'OutputList3Cue',
    sigType: 'OutputList3SigType',
    typeName: 'Serial',
    tip: 'Output3ToolTip',
    direction: 'panel → program',
  });
  lines.push(']');
  lines.push('[');
  lines.push('ObjTp=Dp');
  lines.push('Tp=1');
  lines.push('HD=TRUE');
  lines.push('NF=1');
  lines.push('DNF=1');
  lines.push('EncFmt=0');
  lines.push('DVLF=1');
  lines.push('Sgn=0');
  lines.push(`H=${id}`);
  lines.push(`DV=${id}d`);
  lines.push(']');
  lines.push('[');
  lines.push('ObjTp=CHD');
  lines.push(`H=${id}`);
  lines.push(`ChdCode=${id}`);
  lines.push('ParentChdFolder=0');
  lines.push(']');
  return lines.join('\n');
}

function buildChd() {
  const header = [
    '[',
    'ObjTp=FSgntr',
    'Sgntr=CHD',
    'RelVrs=1',
    'Schema=1',
    ']',
    '[',
    'ObjTp=Hd',
    'Schema=1',
    `ProjectFile=${contractName}`,
    `ContractID=${contractId}`,
    'CEProjectVer=1.0.0.0',
    `DateTimeUTC=${timestamp}`,
    ']',
  ].join('\n');
  return [header, ...components.map(buildChdSymbol)].join('\n');
}

function joinAt(list) {
  const joins = [];
  let join = 1;
  for (const name of list) {
    if (!name) {
      joins.push('');
      continue;
    }
    joins.push(String(join));
    join += 1;
  }
  return joins;
}

function gridTable(title, pair) {
  const inputJoins = joinAt(pair.inputs);
  const outputJoins = joinAt(pair.outputs);
  const lines = [
    `## ${title}`,
    '',
    '| Row | State join | Input (program → panel) | Event join | Output (panel → program) |',
    '| --- | --- | --- | --- | --- |',
  ];
  for (let index = 0; index < pair.inputs.length; index += 1) {
    const row = index + 1;
    const inputName = pair.inputs[index] ?? '[~UNUSED~]';
    const outputName = pair.outputs[index] ?? '[~UNUSED~]';
    lines.push(
      `| ${row} | ${inputJoins[index]} | ${inputName} | ${outputJoins[index]} | ${outputName} |`,
    );
  }
  lines.push('');
  return lines.join('\n');
}

function buildMap() {
  const file = components[0];
  const digitalStates = Object.keys(namedJoins(file.digital.inputs)).length;
  const digitalEvents = Object.keys(namedJoins(file.digital.outputs)).length;
  const analogStates = Object.keys(namedJoins(file.analog.inputs)).length;
  const analogEvents = Object.keys(namedJoins(file.analog.outputs)).length;
  const serialStates = Object.keys(namedJoins(file.serial.inputs)).length;
  const serialEvents = Object.keys(namedJoins(file.serial.outputs)).length;
  return [
    '# File Reader contract',
    '',
    'Generated by `npm run generate:contract`. The UI uses these signal names. Join numbers are the `.cse2j` ids. A `[~UNUSED~]` row is not a join.',
    '',
    `Contract name \`${contractName}\`. Contract id \`${contractId}\`. One symbol, Control Join Id **1**, SIMPL name \`FileReader.FileReader\`.`,
    '',
    'Extender inputs are File Reader outputs (what the panel shows). Extender outputs are File Reader inputs (what the panel sends). Wire the same name on both symbols.',
    '',
    `Digitals: ${digitalStates} states, ${digitalEvents} events. Analogs: ${analogStates} states, ${analogEvents} events. Serials: ${serialStates} states, ${serialEvents} events.`,
    '',
    'These SIMPL parameters are not joins: File Location, Filename, Format, Default Wait Time.',
    '',
    gridTable('Digitals', file.digital),
    gridTable('Analogs', file.analog),
    gridTable('Serials', file.serial),
  ].join('\n');
}

const cse2j = `${JSON.stringify(buildCse2j(), null, '\t')}\n`;
const chd = crlf(`${buildChd()}\n`);
const map = buildMap();

mkdirSync(join(root, 'contracts'), { recursive: true });
mkdirSync(join(root, 'public', 'config'), { recursive: true });
mkdirSync(join(root, 'docs'), { recursive: true });
writeFileSync(join(root, 'contracts', 'file-reader.cse2j'), cse2j, 'utf8');
writeFileSync(join(root, 'public', 'config', 'contract.cse2j'), cse2j, 'utf8');
writeFileSync(join(root, 'contracts', 'file-reader.chd'), chd, 'utf8');
writeFileSync(join(root, 'docs', 'CONTRACT-MAP.md'), map, 'utf8');

console.log('Wrote contracts/file-reader.cse2j');
console.log('Wrote public/config/contract.cse2j');
console.log('Wrote contracts/file-reader.chd');
console.log('Wrote docs/CONTRACT-MAP.md');
