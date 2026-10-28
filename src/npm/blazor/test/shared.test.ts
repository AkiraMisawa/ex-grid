// A product's Blazor module answers its stamp only over the Shared Script it was built against (ADR-0411).

import { describe, expect, it } from 'vitest';
import { sharedStamp, stampAgainst } from '../src/shared';

describe('ADR-0411: a product module refuses a Shared Script it was not built against', () => {
    it('answers its own stamp over the Shared Script it expects', () => {
        expect(stampAgainst('the-module', sharedStamp())).toBe('the-module');
    });

    it('answers no stamp of its own over another build\'s Shared Script, so the component refuses it', () => {
        const answered = stampAgainst('the-module', 'another-shared-script');
        expect(answered).not.toBe('the-module');
        expect(answered).toBe('the Shared Script the-shared-script, not another-shared-script');
    });
});
