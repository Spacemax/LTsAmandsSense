// Script to generate updated Items.json for AmandsSense from SPT 4.0.8 database
const fs = require('fs');
const path = require('path');

// Paths
const itemsDbPath = 'G:\\Installed Games\\SPT\\SPT\\SPT_Data\\database\\templates\\items.json';
const questsDbPath = 'G:\\Installed Games\\SPT\\SPT\\SPT_Data\\database\\templates\\quests.json';
const outputPath = 'G:\\Installed Games\\SPT\\BepInEx\\plugins\\AmandsSense\\Sense\\Items.json';

console.log('Loading items database...');
const itemsDb = JSON.parse(fs.readFileSync(itemsDbPath, 'utf8'));

// NonFleaExclude: Items that CANNOT be sold on flea market
// We want to EXCLUDE from highlighting certain common items like money, ammo boxes, etc.
const nonFleaExclude = [];
const nonFleaItems = []; // Items where CanSellOnRagfair = false

// Common exclusions (money, basic containers that shouldn't trigger non-flea highlight)
const commonExclusions = [
    '5449016a4bdc2d6f028b456f', // Roubles
    '5696686a4bdc2da3298b456a', // Dollars
    '569668774bdc2da2298b4568', // Euros
];

// Rare items - high value items worth highlighting
// These are manually curated based on current game meta
const rareItems = [];

// Item categories to check for rare items
const rareCategories = [
    'Key', // All keys are valuable
    'Keycard', // Keycards
];

// Specific rare item patterns/names
const rarePatterns = [
    'LEDX',
    'GPU',
    'Graphics card',
    'Tetriz',
    'Bitcoin',
    'Virtex',
    'Military',
    'Intelligence',
    'Diary',
    'Folder',
    'SSD',
    'Ophthalmoscope',
    'Defibrillator',
    'AESA',
    'Iridium',
    'Phased array',
    'Tank battery',
    'Prokill',
    'Raven',
    'Rogue',
    'Labs',
    'TerraGroup',
    'Streamer',
    'Rivals',
    'Cultist',
    'Killa',
    'Tagilla',
    'Shturman',
    'Sanitar',
    'Glukhar',
    'Reshala',
];

console.log('Processing items...');
let itemCount = 0;
for (const [itemId, item] of Object.entries(itemsDb)) {
    if (!item._props) continue;
    itemCount++;

    const props = item._props;
    const name = props.Name || props.ShortName || '';
    const parentId = item._parent || '';

    // Check for non-flea items (CanSellOnRagfair = false)
    if (props.CanSellOnRagfair === false && !commonExclusions.includes(itemId)) {
        nonFleaItems.push(itemId);
    }

    // Check for rare items by name patterns
    for (const pattern of rarePatterns) {
        if (name.toLowerCase().includes(pattern.toLowerCase())) {
            if (!rareItems.includes(itemId)) {
                rareItems.push(itemId);
            }
            break;
        }
    }

    // Check for keys (usually valuable)
    if (parentId === '5c99f98d86f7745c314214b3' || // Key
        parentId === '5c164d2286f774194c5e69fa' || // Keycard
        name.includes('key') && props.Rarity === 'Rare') {
        if (!rareItems.includes(itemId)) {
            rareItems.push(itemId);
        }
    }
}

console.log(`Processed ${itemCount} items`);
console.log(`Found ${nonFleaItems.length} non-flea items`);
console.log(`Found ${rareItems.length} rare items`);

// Kappa items - items required for Collector quest (Kappa container)
// These are the find-in-raid items needed for the Collector quest
const kappaItems = [
    // Streamer items
    '5bc9c377d4351e3bac12251b', // Veritas guitar pick
    '5bc9c1e2d4351e00367fbcf0', // Pestily plague mask
    '5bc9c049d4351e44f824d360', // DeadlySlob's beard oil
    '5bc9b355d4351e6d1509862a', // Kotton beanie
    '5bc9bc53d4351e00367fbcee', // Shroud half-mask
    '5bc9bdb8d4351e003562b8a1', // Smoke balaclava
    '5bc9b9ecd4351e3bac122519', // WZ Wallet
    '5bc9b720d4351e450201234b', // Golden 1GPhone
    '5bc9b156d4351e00367fbce9', // Evasion armband
    '5bc9c29cd4351e003562b8a3', // Fake mustache
    '5bd073a586f7747e6f135799', // Sewing kit (Jaeger)
    '5bd073c986f7747f627e796c', // Damaged hard drive
    // Jaeger quest items
    '5e54f62086f774219b0f1937', // MS2000 Marker
    '5e54f79686f7744022011103', // Cultist knife
    '5e54f76986f7740366043752', // Antique book
    '5e54f6af86f7742199090bf3', // Silver Badge
    '5bc9be8fd4351e00334cae6e', // Jar of DevilDog mayo
    // Additional Kappa requirements
    '5f745ee30acaeb0d490d8c5b', // Rogue USEC stash key
    '60b0f988c4449e4cb624c1da', // USEC PMC operator shirt
    '60b0f93284c20f0feb453da7', // BEAR PMC operator shirt
    '60b0f7057897d47c5b04ab94', // Video cassette with Cyrillic
    '5fd8d28367cb5e077335170f', // Microcontroller board
    '60b0f6c058e0b0481a09ad11', // Antique vase
    '60b0f561c4449e4cb624c1d7', // Blue marking tape
    // Lightkeeper items
    '62a09ec84f842e1bd12da3f2', // Electric motor
    '62a09e974f842e1bd12da3f0', // Microprocessor
    '62a09e73af34e73a266d932a', // Printed circuit board
    '62a09e410b9d3c46de5b6e78', // Phase control relay
    '62a09e08de7ac81993580532', // Military power filter
    '62a09dd4621468534a797ac7', // Military COFDM transmitter
    '62a09d79de7ac81993580530', // Military gyrotachometer
    '62a09d3bcf4a99369e262447', // Ultraviolet lamp
    '62a09cfe4f842e1bd12da3e4', // Fleece fabric
    '62a09cb7a04c0c5c6e0a84f8', // Radiator helix
    '62a091170b9d3c46de5b6cf2', // Broken LCD
    '62a08f4c4f842e1bd12d9d62', // Electronic components
];

// Build output
const output = {
    RareItems: rareItems,
    KappaItems: kappaItems,
    NonFleaExclude: commonExclusions
};

console.log('\nWriting Items.json...');
fs.writeFileSync(outputPath, JSON.stringify(output, null, 2));
console.log(`Written to ${outputPath}`);

console.log('\nSummary:');
console.log(`  RareItems: ${output.RareItems.length}`);
console.log(`  KappaItems: ${output.KappaItems.length}`);
console.log(`  NonFleaExclude: ${output.NonFleaExclude.length}`);
