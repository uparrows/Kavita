import { Component, ElementRef, Input, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { Subject } from 'rxjs';
import { take, takeUntil } from 'rxjs/operators';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-api-key',
  templateUrl: './api-key.component.html',
  styleUrls: ['./api-key.component.scss']
})
export class ApiKeyComponent implements OnInit, OnDestroy {

  @Input() title: string = 'API Key';
  @Input() showRefresh: boolean = true;
  @Input() transform: (val: string) => string = (val: string) => val;
  @Input() tooltipText: string = '';
  @ViewChild('apiKey') inputElem!: ElementRef;
  key: string = '';
  private readonly onDestroy = new Subject<void>();
  

  constructor(private confirmService: ConfirmService, private accountService: AccountService, private toastr: ToastrService) { }

  ngOnInit(): void {
    this.accountService.currentUser$.pipe(takeUntil(this.onDestroy)).subscribe(user => {
      let key = '';
      if (user) {
        key = user.apiKey;
      } else {
        key = 'ERROR - KEY NOT SET';
      }

      if (this.transform != undefined) {
        this.key = this.transform(key);
      }
    });
  }

  ngOnDestroy() {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  async copy() {
    await navigator.clipboard.writeText(this.key);
  }

  async refresh() {
    if (!await this.confirmService.confirm('这将使您设置的任何 OPDS 配置无效。 你确定你要继续吗?')) {
      return;
    }
    this.accountService.resetApiKey().subscribe(newKey => {
      this.key = newKey;
      this.toastr.success('API 密钥重置');
    });
  }

  selectAll() {
    if (this.inputElem) {
      this.inputElem.nativeElement.setSelectionRange(0, this.key.length);
    }
  }

}
