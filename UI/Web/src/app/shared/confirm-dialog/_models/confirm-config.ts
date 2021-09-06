import { ConfirmButton } from './confirm-button';

export class ConfirmConfig {
    _type: string = 'confirm'; // internal only: confirm or alert (todo: use enum)
    header: string = '确认';
    content: string = '';
    buttons: Array<ConfirmButton> = [];
}
