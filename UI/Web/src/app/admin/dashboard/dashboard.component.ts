import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { ServerService } from 'src/app/_services/server.service';
import { Title } from '@angular/platform-browser';



@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {

  tabs: Array<{title: string, fragment: string}> = [
    {title: '一般', fragment: ''},
    {title: '用户', fragment: 'users'},
    {title: '书库', fragment: 'libraries'},
    {title: '系统', fragment: 'system'},
    {title: '更新日志', fragment: 'changelog'},
  ];
  counter = this.tabs.length + 1;
  active = this.tabs[0];

  constructor(public route: ActivatedRoute, private serverService: ServerService, 
    private toastr: ToastrService, private titleService: Title) {
    this.route.fragment.subscribe(frag => {
      const tab = this.tabs.filter(item => item.fragment === frag);
      if (tab.length > 0) {
        this.active = tab[0];
      } else {
        this.active = this.tabs[0]; // Default to first tab
      }
    });

  }

  ngOnInit() {
    this.titleService.setTitle('Kavita - 管理控制台');
  }

  restartServer() {
    this.serverService.restart().subscribe(() => {
      setTimeout(() => this.toastr.success('请重新加载。'), 1000);
    });
  }
}
